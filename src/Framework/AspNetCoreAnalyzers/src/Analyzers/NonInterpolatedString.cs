// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Analyzers;

[SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008:Enable analyzer release tracking")]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal class NonInterpolatedString : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor UseHeaderDictionaryPropertiesInsteadOfIndexer = new(
        "TEST",
        "UnnecessaryInterpolation",
        "This string is interpolated but does not have any parameters",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptors.UseHeaderDictionaryPropertiesInsteadOfIndexer);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterOperationAction(context =>
        {
            var interpolatedString = (IInterpolatedStringOperation)context.Operation;
        }, OperationKind.InterpolatedString);

    }
}
