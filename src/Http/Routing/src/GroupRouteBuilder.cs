// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// 
/// </summary>
public class GroupRouteBuilder : IEndpointRouteBuilder, IEndpointConventionBuilder
{
    private readonly RoutePattern _pattern;
    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;

    internal GroupRouteBuilder(RoutePattern pattern, IEndpointRouteBuilder outerEndpointRouteBuilder)
    {
        _pattern = pattern;
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;

        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
    }

    /// <inheritdoc/>
    public IServiceProvider ServiceProvider => throw new NotImplementedException();

    /// <inheritdoc/>
    public ICollection<EndpointDataSource> DataSources => throw new NotImplementedException();

    /// <inheritdoc/>
    public void Add(Action<EndpointBuilder> convention)
    {
        throw new NotImplementedException();
    }

    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder()
    {
        throw new NotSupportedException();
    }

    private class GroupDataSource : EndpointDataSource
    {
        private readonly GroupRouteBuilder _groupRouteBuilder;

        public GroupDataSource(GroupRouteBuilder groupRouteBuilder)
        {
            _groupRouteBuilder = groupRouteBuilder;
        }

        public override IReadOnlyList<Endpoint> Endpoints => throw new NotImplementedException();

        public override IChangeToken GetChangeToken()
        {
            throw new NotImplementedException();
        }
    }
}
