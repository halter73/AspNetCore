// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers.Mvc;

using WellKnownType = WellKnownTypeData.WellKnownType;

public partial class MvcAnalyzer
{
    private static void DetectOverriddenAuthorizeAttribute(SymbolAnalysisContext context, WellKnownTypes wellKnownTypes, INamedTypeSymbol controllerSymbol, IMethodSymbol actionSymbol)
    {
        var authAttributeData = actionSymbol.GetAttributes(wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAuthorizeData)).FirstOrDefault();

        var authAttributeLocation = authAttributeData?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
        if (authAttributeLocation is null)
        {
            return;
        }

        var anonymousControllerType = GetNearestTypeWithAttribute(controllerSymbol, wellKnownTypes.Get(WellKnownType.Microsoft_AspNetCore_Authorization_IAllowAnonymous));
        if (anonymousControllerType is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.AuthorizeAttributeOverridden,
            authAttributeLocation,
            anonymousControllerType.Name));
    }

    private static ITypeSymbol? GetNearestTypeWithAttribute(ITypeSymbol? typeSymbol, ITypeSymbol attributeInterface)
    {
        while (typeSymbol is not null)
        {
            if (typeSymbol.GetAttributes(attributeInterface).Any(IsAttributeInherited))
            {
                return typeSymbol;
            }
            typeSymbol = typeSymbol.BaseType;
        }

        return null;
    }

    private static bool IsAttributeInherited(AttributeData attributeData)
    {
        var attributeUsage = attributeData.AttributeClass?.GetAttributes()
            .FirstOrDefault(ad =>
                ad.AttributeClass?.Name == nameof(AttributeUsageAttribute) &&
                ad.AttributeClass.ContainingNamespace.Name == "System");

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

        // If the AttributeUsage is not found or the Inherited property is not set,
        // the default is true for attribute inheritance.
        return true;
    }
}
