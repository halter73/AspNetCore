// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.App.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Http.Generators.StaticRouteHandlerModel;

internal class EndpointParameter
{
    public EndpointParameter(IParameterSymbol parameter)
    {
        Type = parameter.Type;
        Name = parameter.Name;
        Source = EndpointParameterSource.Unknown;
    }

    public ITypeSymbol Type { get; init; }
    public EndpointParameterSource Source { get; init; }

    // TODO: If the parameter has [FromRoute("AnotherName")] or similar, prefer that.
    public string Name { get; init; }
}
