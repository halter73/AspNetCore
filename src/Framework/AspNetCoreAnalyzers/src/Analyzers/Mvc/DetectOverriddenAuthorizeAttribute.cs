// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

using WellKnownType = WellKnownTypeData.WellKnownType;

public partial class MvcAnalyzer
{
    /// <summary>
    /// This tries to detect [Authorize] attributes that are unwittingly overridden by [AllowAnonymous] attributes that are "farther" away from a controller.
    /// </summary>
    /// <remarks>
    /// This might report the same [Authorize] attribute multiple times if it's on a shared base type, but we'd have to disable parallelization of the
    /// entire MvcAnalyzer to avoid that. We assume that this scenario is rare enough and that overreporting is benign enough to not warrant the performance hit.
    /// See AuthorizeOnControllerBaseWithMultipleChildren_AllowAnonymousOnControllerBaseBaseType_HasMultipleDiagnostics.
    /// </remarks>
    private static void DetectOverriddenAuthorizeAttributeOnController(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes,
        INamedTypeSymbol controllerSymbol, List<AttributeInfo> authorizeAttributes, out string? allowAnonClass)
    {
        Debug.Assert(authorizeAttributes.Count is 0);

        var isCheckingBaseType = false;
        allowAnonClass = null;

        foreach (var currentClass in controllerSymbol.GetTypeHierarchy())
        {
            FindAuthorizeAndAllowAnonymous(wellKnownTypes, currentClass, isCheckingBaseType, authorizeAttributes, out var foundAllowAnonymous);
            if (foundAllowAnonymous)
            {
                // Anything we find after this would be farther away, so we can short circuit.
                ReportAuthorizeAttributeOverriddenDiagnosticsIfAny(context, authorizeAttributes, currentClass.Name);
                // Keep track of the nearest class with [AllowAnonymous] for later reporting of method-level [Authorize] attributes.
                allowAnonClass = currentClass.Name;
                return;
            }

            isCheckingBaseType = true;
        }
    }

    /// <summary>
    /// This tries to detect [Authorize] attributes that are unwittingly overridden by [AllowAnonymous] attributes that are "farther" away from a controller action.
    /// To do so, it first searches the action method and then the controller class. It repeats this process for each virtual method the action may override and for
    /// each base class the controller may inherit from. Since it searches for the attributes closest to the action first, it short circuits as soon as [AllowAnonymous] is found.
    /// If it has already detected a closer [Authorize] attribute, it reports a diagnostic at the [Authorize] attribute's location indicating that it will be overridden.
    /// </summary>
    private static void DetectOverriddenAuthorizeAttributeOnAction(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes,
        IMethodSymbol actionSymbol, List<AttributeInfo> authorizeAttributes, string? allowAnonClass)
    {
        Debug.Assert(authorizeAttributes.Count is 0);

        bool foundAllowAnonymous;
        var isCheckingBaseType = false;
        var currentMethod = actionSymbol;

        foreach (var currentClass in actionSymbol.ContainingType.GetTypeHierarchy())
        {
            if (currentMethod is not null && IsSameSymbol(currentMethod.ContainingType, currentClass))
            {
                FindAuthorizeAndAllowAnonymous(wellKnownTypes, currentMethod, isCheckingBaseType, authorizeAttributes, out foundAllowAnonymous);
                if (foundAllowAnonymous)
                {
                    // [AllowAnonymous] was found on the action method. Anything we find after this would be farther away, so we don't need to report any
                    // [Authorize] attributes unless we already found one on the very same method or an override.
                    ReportAuthorizeAttributeOverriddenDiagnosticsIfAny(context, authorizeAttributes,
                        $"{currentMethod.ContainingType.Name}.{currentMethod.Name}");
                    return;
                }

                if (!isCheckingBaseType && allowAnonClass is not null)
                {
                    // Don't use allowAnonClass once we start checking overrides to avoid wonkiness. But if we found [Authorize] directly on a non-inherited
                    // action method, we can report it without rechecking the controller or its base types for [AllowAnonymous] if one was passed in.
                    ReportAuthorizeAttributeOverriddenDiagnosticsIfAny(context, authorizeAttributes, allowAnonClass);
                    // Continue to report any [Authorize] attributes on any base methods.
                    authorizeAttributes.Clear();
                }

                currentMethod = currentMethod.OverriddenMethod;

                if (currentMethod is null && authorizeAttributes.Count is 0)
                {
                    // We've already checked the Controller and any base classes for overridden attributes in DetectOverriddenAuthorizeAttributeOnController.
                    // If there are no more base methods and are not tracking any unreported [Authorize] attributes that might be overridden by a class, we're done.
                    return;
                }
            }

            FindAuthorizeAndAllowAnonymous(wellKnownTypes, currentClass, isCheckingBaseType, authorizeAttributes, out foundAllowAnonymous);
            if (foundAllowAnonymous)
            {
                // We've already searched Controllers and their base types for overridden [Authorize] attribute locations, we don't need to report those again.
                // We're just looking for [Authorize] on virtual methods and verifying they are not overridden by [AllowAnonymous] attributes on classes.
                // We still need to track [Authorize] attributes on classes in case there is a [AllowAnonymous] on a base method farther away.
                ReportAuthorizeAttributeOverriddenDiagnosticsIfAny(context, authorizeAttributes.Where(a => a.IsTargetingMethod), currentClass.Name);
                return;
            }

            // Any [Authorize] attributes we find from here on out might be overridden by a different [AllowAnonymous] than the one on allowAnonClass
            // like one on a virtual base method.
            isCheckingBaseType = true;
        }

        Debug.Assert(currentMethod is null);
    }

    private static bool IsSameSymbol(ISymbol? x, ISymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);

    private static bool IsInheritableAttribute(WellKnownTypes wellKnownTypes, INamedTypeSymbol attribute)
    {
        // [AttributeUsage] is sealed but inheritable.
        var attributeUsageAttributeType = wellKnownTypes.Get(WellKnownType.System_AttributeUsageAttribute);
        var attributeUsage = attribute.GetAttributes(attributeUsageAttributeType, inherit: true).FirstOrDefault();

        if (attributeUsage is not null)
        {
            foreach (var arg in attributeUsage.NamedArguments)
            {
                if (arg.Key == nameof(AttributeUsageAttribute.Inherited))
                {
                    return (bool)arg.Value.Value!;
                }
            }
        }

        // If [AttributeUsage] is not found or the Inherited property is not set, the default is true.
        return true;
    }

    private static bool IsMatchingAttribute(WellKnownTypes wellKnownTypes, INamedTypeSymbol attribute,
        INamedTypeSymbol commonAttribute, ITypeSymbol attributeInterface, bool mustBeInheritable)
    {
        // The "common" attribute is either [Authorize] or [AllowAnonymous] so we can skip the interface and inheritable checks.
        if (IsSameSymbol(attribute, commonAttribute))
        {
            return true;
        }

        if (!attributeInterface.IsAssignableFrom(attribute))
        {
            return false;
        }

        return !mustBeInheritable || IsInheritableAttribute(wellKnownTypes, attribute);
    }

    private static void FindAuthorizeAndAllowAnonymous(WellKnownTypes wellKnownTypes, ISymbol symbol, bool isCheckingBaseType,
        List<AttributeInfo> authorizeAttributes, out bool foundAllowAnonymous)
    {
        AttributeData? localAuthorizeAttribute = null;
        List<AttributeData>? localAuthorizeAttributeOverflow = null;
        foundAllowAnonymous = false;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            var authInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAuthorizeData);
            var authAttributeType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_AuthorizeAttribute);
            if (IsMatchingAttribute(wellKnownTypes, attribute.AttributeClass, authAttributeType, authInterfaceType, isCheckingBaseType))
            {
                if (localAuthorizeAttribute is null)
                {
                    localAuthorizeAttribute = attribute;
                }
                else
                {
                    // This is ony allocated if there are multiple of either [Authorize] attributes on the same symbol which we assume is rare.
                    localAuthorizeAttributeOverflow ??= [];
                    localAuthorizeAttributeOverflow.Add(attribute);
                }
            }

            var anonInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous);
            var anonAttributeType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_AllowAnonymousAttribute);
            if (IsMatchingAttribute(wellKnownTypes, attribute.AttributeClass, anonAttributeType, anonInterfaceType, isCheckingBaseType))
            {
                // If localAuthorizeAttribute is not null, [AllowAnonymous] came after [Authorize] on the same method or class. We assume
                // this closer [AllowAnonymous] was intended to override the [Authorize] attribute which it always does regardless of order.
                localAuthorizeAttribute = null;
                localAuthorizeAttributeOverflow?.Clear();
                foundAllowAnonymous = true;
            }
        }

        if (localAuthorizeAttribute is not null)
        {
            var isTargetingMethod = symbol is IMethodSymbol;
            authorizeAttributes.Add(new(localAuthorizeAttribute, isTargetingMethod));
            if (localAuthorizeAttributeOverflow is not null)
            {
                foreach (var extraAttribute in localAuthorizeAttributeOverflow)
                {
                    authorizeAttributes.Add(new(extraAttribute, isTargetingMethod));
                }
            }
        }
    }

    private static void ReportAuthorizeAttributeOverriddenDiagnosticsIfAny(SymbolAnalysisContext context,
        IEnumerable<AttributeInfo> authorizeAttributes, string allowAnonymousLocation)
    {
        foreach (var authorizeAttribute in authorizeAttributes)
        {
            if (authorizeAttribute.AttributeData.ApplicationSyntaxReference is { } syntaxReference)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AuthorizeAttributeOverridden,
                    syntaxReference.GetSyntax(context.CancellationToken).GetLocation(),
                    allowAnonymousLocation));
            }
        }
    }
}
