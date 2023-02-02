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

    public bool SignatureEquals(Endpoint other)
    {
        // Eventually we may not have to compare HttpMethod because it should only influence parameter sources
        // which is compared as part of the Parameters array.
        if (!other.HttpMethod.Equals(HttpMethod, StringComparison.Ordinal) ||
            !other.Response.Equals(Response) ||
            other.Parameters.Length != Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < Parameters.Length; i++)
        {
            if (other.Parameters[i] != Parameters[i])
            {
                return false;
            }
        }

        return true;
    }

    public int GetSignatureHashCode()
    {
        unchecked
        {
            var hashCode = HttpMethod.GetHashCode();
            hashCode = (hashCode * 397) ^ HttpMethod.GetHashCode();
            hashCode = (hashCode * 397) ^ Response.GetHashCode();

            foreach (var parameter in Parameters)
            {
                hashCode = (hashCode * 397) ^ parameter.GetHashCode();
            }

            return hashCode;
        }
    }

    public override bool Equals(object obj) =>
        obj is Endpoint other && other.Location.Equals(Location) && SignatureEquals(other);

    public override int GetHashCode() =>
        unchecked((Location.GetHashCode() * 397) ^ GetSignatureHashCode());
}
