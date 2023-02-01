// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Analyzers.Infrastructure;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;

namespace Microsoft.AspNetCore.Http.Generators;

[Generator]
public sealed class RequestDelegateGenerator : IIncrementalGenerator
{
    private static readonly string[] _knownMethods =
    {
        "MapGet",
        "MapPost",
        "MapPut",
        "MapDelete",
        "MapPatch",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var endpointsWithDiagnostics = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, _) => node is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: IdentifierNameSyntax
                    {
                        Identifier: { ValueText: var method }
                    }
                },
                ArgumentList: { Arguments: { Count: 2 } args }
            } && _knownMethods.Contains(method),
            transform: (context, token) =>
            {
                var operation = context.SemanticModel.GetOperation(context.Node, token) as IInvocationOperation;
                var wellKnownTypes = WellKnownTypes.GetOrCreate(context.SemanticModel.Compilation);
                return new Endpoint(operation, wellKnownTypes);
            })
            .WithTrackingName(GeneratorSteps.EndpointsStep);

        context.RegisterSourceOutput(endpointsWithDiagnostics, (context, endpoint) =>
        {
            var (filePath, _) = endpoint.Location;
            foreach (var diagnostic in endpoint.Diagnostics)
            {
                context.ReportDiagnostic(Diagnostic.Create(diagnostic, endpoint.Operation.Syntax.GetLocation(), filePath));
            }
        });

        var endpoints = endpointsWithDiagnostics
            .Where(endpoint => endpoint.Diagnostics.Count == 0)
            .WithTrackingName(GeneratorSteps.EndpointsWithoutDiagnosicsStep);

        var thunks = endpoints.Select((endpoint, _) => $$"""
            [{{endpoint.EmitSourceKey()}}] = (
               (methodInfo, options) =>
                {
                    if (options == null || options.EndpointBuilder == null)
                    {
                        return new RequestDelegateMetadataResult { EndpointMetadata = ReadOnlyCollection<object>.Empty };
                    }
                    options.EndpointBuilder.Metadata.Add(new SourceKey{{endpoint.EmitSourceKey()}});
                    return new RequestDelegateMetadataResult { EndpointMetadata = options.EndpointBuilder.Metadata.AsReadOnly() };
                },
                (del, options, inferredMetadataResult) =>
                {
                    var handler = ({{endpoint.EmitHandlerDelegateType()}})del;
                    EndpointFilterDelegate? filteredInvocation = null;

                    if (options?.EndpointBuilder?.FilterFactories.Count > 0)
                    {
                        filteredInvocation = GeneratedRouteBuilderExtensionsCore.BuildFilterDelegate(ic =>
                        {
                            if (ic.HttpContext.Response.StatusCode == 400)
                            {
                                return ValueTask.FromResult<object?>(Results.Empty);
                            }
{{endpoint.EmitFilteredInvocation()}}
                        },
                        options.EndpointBuilder,
                        handler.Method);
                    }

{{endpoint.EmitRequestHandler()}}
{{StaticRouteHandlerModelEmitter.EmitFilteredRequestHandler()}}

                    RequestDelegate targetDelegate = filteredInvocation is null ? RequestHandler : RequestHandlerFiltered;
                    var metadata = inferredMetadataResult?.EndpointMetadata ?? ReadOnlyCollection<object>.Empty;
                    return new RequestDelegateResult(targetDelegate, metadata);
                }),
""");

        var stronglyTypedEndpointDefinitions = endpoints
            .Collect()
            .Select((endpoints, _) =>
            {
                var dedupedByDelegate = endpoints.Distinct(new LambdaComparer<Endpoint>((a, b) =>
                {
                    if (a.Response.IsAwaitable == b.Response.IsAwaitable &&
                        a.Response.IsVoid == b.Response.IsVoid &&
                        SymbolEqualityComparer.Default.Equals(a.Response.ResponseType, b.Response.ResponseType) &&
                        a.HttpMethod == b.HttpMethod)
                    {
                        return 0;
                    }
                    return -1;
                }, (endpoint) =>
                {
                    unchecked
                    {
                        var hashCode = SymbolEqualityComparer.Default.GetHashCode(endpoint.Response.ResponseType);
                        hashCode = (hashCode * 397) ^ endpoint.Response.IsAwaitable.GetHashCode();
                        hashCode = (hashCode * 397) ^ endpoint.Response.IsVoid.GetHashCode();
                        hashCode = (hashCode * 397) ^ endpoint.HttpMethod.GetHashCode();
                        return hashCode;
                    }
                }));
                var code = new CodeWriter(new StringBuilder());
                code.Indent(2);
                foreach (var endpoint in dedupedByDelegate)
                {
                    code.WriteLine($"internal static global::Microsoft.AspNetCore.Builder.RouteHandlerBuilder {endpoint.HttpMethod}(");
                    code.Indent();
                    code.WriteLine("this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints,");
                    code.WriteLine(@"[global::System.Diagnostics.CodeAnalysis.StringSyntax(""Route"")] string pattern,");
                    code.WriteLine($"global::{endpoint.EmitHandlerDelegateType()} handler,");
                    code.WriteLine(@"[global::System.Runtime.CompilerServices.CallerFilePath] string filePath = """",");
                    code.WriteLine("[global::System.Runtime.CompilerServices.CallerLineNumber]int lineNumber = 0)");
                    code.Unindent();
                    code.StartBlock();
                    code.WriteLine($"return global::Microsoft.AspNetCore.Http.Generated.GeneratedRouteBuilderExtensionsCore.MapCore(endpoints, pattern, handler, {endpoint.EmitVerb()}, filePath, lineNumber);");
                    code.EndBlock();
                }

                return code.ToString();
            });

        var thunksAndEndpoints = thunks.Collect().Combine(stronglyTypedEndpointDefinitions);

        context.RegisterSourceOutput(thunksAndEndpoints, (context, sources) =>
        {
            var (thunks, endpointsCode) = sources;

            if (thunks.IsDefaultOrEmpty || string.IsNullOrEmpty(endpointsCode))
            {
                return;
            }

            var thunksCode = new CodeWriter(new StringBuilder());

            foreach (var thunk in thunks)
            {
                thunksCode.WriteLine(thunk);
            }

            var code = RequestDelegateGeneratorSources.GetGeneratedRouteBuilderExtensionsSource(
                genericThunks: string.Empty,
                thunks: thunksCode.ToString(),
                endpoints: endpointsCode);

            context.AddSource("GeneratedRouteBuilderExtensions.g.cs", code);
        });
    }
}
