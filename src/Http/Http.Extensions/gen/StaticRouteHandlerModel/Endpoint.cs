// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;

internal class Endpoint
{
    public string HttpMethod { get; }
    public string? RoutePattern { get; }
    public EndpointResponse? Response { get; }
    public IEnumerable<EndpointParameter> Parameters { get; } = Array.Empty<EndpointParameter>();

    public List<DiagnosticDescriptor> Diagnostics { get; } = new List<DiagnosticDescriptor>();

    public (string, int) Location { get; }
    public IInvocationOperation Operation { get; }

    private WellKnownTypes WellKnownTypes { get; }

    public Endpoint(IInvocationOperation operation, WellKnownTypes wellKnownTypes)
    {
        Operation = operation;
        WellKnownTypes = wellKnownTypes;
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
            var parameter = new EndpointParameter(method.Parameters[i]);

            if (parameter.Source == EndpointParameterSource.Unknown)
            {
                Diagnostics.Add(DiagnosticDescriptors.GetUnableToResolveParameterDescriptor(parameter.Name));
                return;
            }
        }

        Parameters = parameters;
    }

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
            return endpoint.HttpMethod.Equals(HttpMethod, StringComparison.OrdinalIgnoreCase) ||
                endpoint.Location.Item1.Equals(Location.Item1, StringComparison.OrdinalIgnoreCase) ||
                endpoint.Location.Item2.Equals(Location.Item2) ||
                endpoint.Response.Equals(Response);
        }

        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = HttpMethod.GetHashCode();
            hashCode = (hashCode * 397) ^ Response.GetHashCode();
            hashCode = (hashCode * 397) ^ Diagnostics.GetHashCode();
            hashCode = (hashCode * 397) ^ Location.GetHashCode();
            hashCode = (hashCode * 397) ^ Operation.GetHashCode();
            return hashCode;
        }
    }
}
