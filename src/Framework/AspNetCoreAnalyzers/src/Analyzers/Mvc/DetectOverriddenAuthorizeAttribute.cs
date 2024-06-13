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
    /// This tries to detect [Authorize] attributes that are unwittingly overridden by [AllowAnonymous] attributes that are "farther" away from a controller action.
    /// To do so, this method searches for any attributes implementing IAuthorizeData or IAllowAnonymous, like [Authorize] and [AllowAnonymous] respectively. It
    /// first search the action method and then the controller class. It repeats this process for each virtual method the action may override and for each base class
    /// the controller may inherit from. Since it searches for the attributes closest to the action first, it short circuits as soon as [AllowAnonymous] is found.
    /// If it already detected a closer [Authorize] attribute already found, it reports a diagnostic at the [Authorize] attribute's location indicating that it will be overridden.
    /// </summary>
    private static void DetectOverriddenAuthorizeAttribute(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes, INamedTypeSymbol controllerSymbol, IMethodSymbol actionSymbol)
    {
        Location? authorizeAttributeLocation = null;
        var isCheckingBaseType = false;

        bool IsAttributeInherited(AttributeData attribute)
        {
            // [AttributeUsage] is sealed but inheritable.
            var attributeUsageAttributeType = wellKnownTypes.Get(WellKnownType.System_AttributeUsageAttribute);
            var attributeUsage = attribute.AttributeClass?.GetAttributes(attributeUsageAttributeType, inherit: true).FirstOrDefault();

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

        // Returns true if the symbol has an [AllowAnonymousAttribute] directly on it. This also tracks the closest [Authorize] attribute found so far.
        bool HasAllowAnonymousAttribute(ISymbol symbol)
        {
            var foundAllowAnonymous = false;
            Location? localAuthorizeAttributeLocation = null;

            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                {
                    continue;
                }

                var authInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAuthorizeData);
                if (authInterfaceType.IsAssignableFrom(attribute.AttributeClass))
                {
                    var authAttributeType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_AuthorizeAttribute);
                    if (!isCheckingBaseType || IsSameSymbol(authAttributeType, attribute.AttributeClass) || IsAttributeInherited(attribute))
                    {
                        localAuthorizeAttributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                    }
                }

                var anonInterfaceType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous);
                if (anonInterfaceType.IsAssignableFrom(attribute.AttributeClass))
                {
                    var anonAttributeType = wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_AllowAnonymousAttribute);
                    if (!isCheckingBaseType || IsSameSymbol(anonAttributeType, attribute.AttributeClass) || IsAttributeInherited(attribute))
                    {
                        // If localAuthorizeAttributeLocation is not null, [AllowAnonymous] came after [Authorize] on the same method or class. We assume
                        // this closer [AllowAnonymous] was intended to override the [Authorize] attribute which it always does regardless of order.
                        // authorizeAttributeLocation is never reset because that must be closer than what we're looking at if it's already set.
                        localAuthorizeAttributeLocation = null;
                        foundAllowAnonymous = true;
                    }
                }
            }

            // This is called on closer attributes before farther ones. Keep the closer [Authorize] location for the diagnostic if one has already been found.
            authorizeAttributeLocation ??= localAuthorizeAttributeLocation;
            return foundAllowAnonymous;
        }

        var currentMethod = actionSymbol;

        foreach (var currentClass in controllerSymbol.GetTypeHierarchy())
        {
            if (currentMethod is not null && IsSameSymbol(currentMethod.ContainingType, currentClass))
            {
                if (HasAllowAnonymousAttribute(currentMethod))
                {
                    if (authorizeAttributeLocation is not null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.AuthorizeAttributeOverridden,
                            authorizeAttributeLocation,
                            $"{currentMethod.ContainingType.Name}.{currentMethod.Name}"));
                    }

                    return;
                }

                currentMethod = currentMethod.IsOverride ? currentMethod.OverriddenMethod : null;
            }

            if (HasAllowAnonymousAttribute(currentClass))
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

            isCheckingBaseType = true;
        }

        Debug.Assert(currentMethod is null);
    }

    private static bool IsSameSymbol(ISymbol? x, ISymbol? y) => SymbolEqualityComparer.Default.Equals(x, y);
}
