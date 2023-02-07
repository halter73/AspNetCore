// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;

internal class Endpoint
{
    private string? _argumentListCache;

    public Endpoint(IInvocationOperation operation, WellKnownTypes wellKnownTypes)
    {
        Operation = operation;
        Location = GetLocation();
        HttpMethod = GetHttpMethod();

        if (!operation.TryGetRouteHandlerPattern(out var routeToken))
        {
            Diagnostics.Add(DiagnosticDescriptors.UnableToResolveRoutePattern);
            return;
        }

        RoutePattern = routeToken.ValueText;

        if (!operation.TryGetRouteHandlerMethod(out var method))
        {
            Diagnostics.Add(DiagnosticDescriptors.UnableToResolveMethod);
            return;
        }

        Response = new EndpointResponse(method, wellKnownTypes);

        if (method.Parameters.Length == 0)
        {
            return;
        }

        var parameters = new EndpointParameter[method.Parameters.Length];

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = new EndpointParameter(method.Parameters[i], wellKnownTypes);

            if (parameter.Source == EndpointParameterSource.Unknown)
            {
                Diagnostics.Add(DiagnosticDescriptors.GetUnableToResolveParameterDescriptor(parameter.Name));
                return;
            }

            parameters[i] = parameter;
        }

        Parameters = parameters;
    }

    public string HttpMethod { get; }
    public string? RoutePattern { get; }
    public EndpointResponse? Response { get; }
    public EndpointParameter[] Parameters { get; } = Array.Empty<EndpointParameter>();
    public string EmitArgumentList() => _argumentListCache ??= string.Join(", ", Parameters.Select(p => p.EmitArgument()));

    public List<DiagnosticDescriptor> Diagnostics { get; } = new List<DiagnosticDescriptor>();

    public (string, int) Location { get; }
    public IInvocationOperation Operation { get; }

    private (string, int) GetLocation()
    {
        var filePath = Operation.Syntax.SyntaxTree.FilePath;
        var span = Operation.Syntax.SyntaxTree.GetLineSpan(Operation.Syntax.Span);
        var lineNumber = span.EndLinePosition.Line + 1;
        return (filePath, lineNumber);
    }

    private string GetHttpMethod()
    {
        var syntax = (InvocationExpressionSyntax)Operation.Syntax;
        var expression = (MemberAccessExpressionSyntax)syntax.Expression;
        var name = (IdentifierNameSyntax)expression.Name;
        var identifier = name.Identifier;
        return identifier.ValueText;
    }

    public override bool Equals(object o)
    {
        if (o is null)
        {
            return false;
        }

        if (o is Endpoint endpoint)
        {
            return endpoint.HttpMethod.Equals(HttpMethod, StringComparison.OrdinalIgnoreCase) &&
                endpoint.Location.Item1.Equals(Location.Item1, StringComparison.OrdinalIgnoreCase) &&
                endpoint.Location.Item2.Equals(Location.Item2) &&
                endpoint.Response.Equals(Response) &&
                endpoint.Diagnostics.SequenceEqual(Diagnostics);
        }

        return false;
    }

    public override int GetHashCode() =>
        HashCode.Combine(HttpMethod, RoutePattern, Location, Response, Diagnostics);
}
