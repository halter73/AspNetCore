// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// 
/// </summary>
public sealed class GroupRouteBuilder : IEndpointRouteBuilder, IEndpointConventionBuilder
{
    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;
    private readonly RoutePattern _pattern;

    private readonly List<Action<EndpointBuilder>> _conventions;

    internal GroupRouteBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern pattern)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;
        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
        _pattern = pattern;

        if (outerEndpointRouteBuilder is GroupRouteBuilder outerGroup)
        {
            GroupPrefix = RoutePatternFactory.Parse(outerGroup.GroupPrefix.RawText + pattern.RawText);
        }
        else
        {
            GroupPrefix = pattern;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public RoutePattern GroupPrefix { get; }

    /// <inheritdoc/>
    public IServiceProvider ServiceProvider => _outerEndpointRouteBuilder.ServiceProvider;

    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() => _outerEndpointRouteBuilder.CreateApplicationBuilder();

    /// <inheritdoc/>
    public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

    /// <inheritdoc/>
    public void Add(Action<EndpointBuilder> convention)
    {
        _conventions.Add(convention);
    }

    private sealed class GroupDataSource : EndpointDataSource
    {
        private readonly GroupRouteBuilder _groupRouteBuilder;

        public GroupDataSource(GroupRouteBuilder groupRouteBuilder)
        {
            _groupRouteBuilder = groupRouteBuilder;
        }

        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                var groupEndpointBuilder = new GroupEndpointBuilder();

                foreach (var convention in _groupRouteBuilder._conventions)
                {
                    convention(groupEndpointBuilder);
                }

                var list = new List<Endpoint>();

                foreach (var dataSource in _groupRouteBuilder.DataSources)
                {
                    foreach (var endpoint in dataSource.Endpoints)
                    {
                        var endpointToAdd = endpoint;

                        if (endpointToAdd is RouteEndpoint routeEndpoint)
                        {
                            endpointToAdd = new RouteEndpoint(
                                // This cannot be null given a RouteEndpoint.
                                routeEndpoint.RequestDelegate!,
                                // TODO: Make sure this works in all cases. What if RawText doesn't end in '/'?
                                RoutePatternFactory.Parse(_groupRouteBuilder._pattern.RawText + routeEndpoint.RoutePattern.RawText),
                                routeEndpoint.Order,
                                routeEndpoint.Metadata,
                                routeEndpoint.DisplayName);
                        }

                        list.Add(endpointToAdd);
                    }
                }

                return list;
            }
        }

        public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }

    private class GroupEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build()
        {
            throw new NotSupportedException();
        }
    }
}
