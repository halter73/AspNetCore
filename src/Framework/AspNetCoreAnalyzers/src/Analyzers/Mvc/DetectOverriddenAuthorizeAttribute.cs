// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    /// <returns>The name of the controller or base class with [AllowAnonymous] if any.</returns>
    private static void DetectOverriddenAuthorizeAttributeOnController(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes,
        INamedTypeSymbol controllerSymbol, out string? allowAnonClass)
    {
        Location? authorizeAttributeLocation = null;
        var isCheckingBaseType = false;
        allowAnonClass = null;

        foreach (var currentClass in controllerSymbol.GetTypeHierarchy())
        {
            FindAuthorizeAndAllowAnonymous(context, wellKnownTypes, currentClass, isCheckingBaseType, ref authorizeAttributeLocation, out var foundAllowAnonymous);
            if (foundAllowAnonymous)
            {
                // Anything we find after this would be farther away, so we can short circuit.
                if (authorizeAttributeLocation is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AuthorizeAttributeOverridden,
                        authorizeAttributeLocation,
                        currentClass.Name));
                }

                allowAnonClass = currentClass.Name;
                return;
            }

            isCheckingBaseType = true;
        }
    }

    /// <summary>
    /// This tries to detect [Authorize] attributes that are unwittingly overridden by [AllowAnonymous] attributes that are "farther" away from a controller action.
    /// To do so, this method searches for any attributes implementing IAuthorizeData or IAllowAnonymous, like [Authorize] and [AllowAnonymous] respectively. It
    /// first search the action method and then the controller class. It repeats this process for each virtual method the action may override and for each base class
    /// the controller may inherit from. Since it searches for the attributes closest to the action first, it short circuits as soon as [AllowAnonymous] is found.
    /// If it already detected a closer [Authorize] attribute found, it reports a diagnostic at the [Authorize] attribute's location indicating that it will be overridden.
    /// </summary>
    /// <returns>True if an [AllowAnonymous] attribute was found on the action method or controller class.</returns>
    private static void DetectOverriddenAuthorizeAttributeOnAction(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes,
        IMethodSymbol actionSymbol, string? allowAnonClass)
    {
        Location? authorizeAttributeLocation = null;
        var isCheckingBaseType = false;
        var currentMethod = actionSymbol;
        bool foundAllowAnonymous;

        foreach (var currentClass in actionSymbol.ContainingType.GetTypeHierarchy())
        {
            if (currentMethod is not null && IsSameSymbol(currentMethod.ContainingType, currentClass))
            {
                FindAuthorizeAndAllowAnonymous(context, wellKnownTypes, currentMethod, isCheckingBaseType, ref authorizeAttributeLocation, out foundAllowAnonymous);
                if (foundAllowAnonymous)
                {
                    // [AllowAnonymous] was found on the action method. Anything we find after this would be farther away,
                    // so we don't need to report any [Authorize] attributes unless we already found one.
                    if (authorizeAttributeLocation is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AuthorizeAttributeOverridden,
                            authorizeAttributeLocation,
                            $"{currentMethod.ContainingType.Name}.{currentMethod.Name}"));
                    }

                    return;
                }

                if (allowAnonClass is not null && authorizeAttributeLocation is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AuthorizeAttributeOverridden,
                        authorizeAttributeLocation,
                        allowAnonClass));
                    return;
                }

                currentMethod = currentMethod.OverriddenMethod;

                if (currentMethod is null && authorizeAttributeLocation is null)
                {
                    // We've already checked the Controller and any base classes for overridden attributes in DetectOverriddenAuthorizeAttributeOnController.
                    // If there are no method-level [Authorize] attributes that could be unexpectedly overridden, we're done.
                    return;
                }
            }

            FindAuthorizeAndAllowAnonymous(context, wellKnownTypes, currentClass, isCheckingBaseType, ref authorizeAttributeLocation, out foundAllowAnonymous);
            if (foundAllowAnonymous)
            {
                if (authorizeAttributeLocation is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AuthorizeAttributeOverridden,
                        authorizeAttributeLocation,
                        $"{currentClass.Name}"));
                }

                return;
            }

            // Any [Authorize] attributes we find from here on out might be overridden by a different [AllowAnonymous] than the one on allowAnonClass
            // like one on a virtual base method.
            allowAnonClass = null;
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

    private static void FindAuthorizeAndAllowAnonymous(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes,
        ISymbol symbol, bool isCheckingBaseType, ref Location? authorizeAttributeLocation, out bool foundAllowAnonymous)
    {
        Location? localAuthorizeAttributeLocation = null;
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
                localAuthorizeAttributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
            }

            var anonInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous);
            var anonAttributeType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_AllowAnonymousAttribute);
            if (IsMatchingAttribute(wellKnownTypes, attribute.AttributeClass, anonAttributeType, anonInterfaceType, isCheckingBaseType))
            {
                // If localAuthorizeAttributeLocation is not null, [AllowAnonymous] came after [Authorize] on the same method or class. We assume
                // this closer [AllowAnonymous] was intended to override the [Authorize] attribute which it always does regardless of order.
                localAuthorizeAttributeLocation = null;
                foundAllowAnonymous = true;
            }
        }

        // This is called on closer attributes before farther ones. Keep the closer [Authorize] location for the diagnostic if one has already been found.
        authorizeAttributeLocation ??= localAuthorizeAttributeLocation;
    }
}
