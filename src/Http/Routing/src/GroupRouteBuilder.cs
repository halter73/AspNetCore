// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// A builder for defining groups of endpoints with a common prefix that implements both the <see cref="IEndpointRouteBuilder"/>
/// and <see cref="IEndpointConventionBuilder"/> interfaces. This can be used to add endpoints with the given <see cref="GroupPrefix"/>,
/// and to customize those endpoints using conventions.
/// </summary>
public sealed class GroupRouteBuilder : IEndpointRouteBuilder, IEndpointConventionBuilder
{
    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;
    private readonly RoutePattern _pattern;

    private readonly List<EndpointDataSource> _dataSources = new();
    private readonly GroupEndpointBuilder _groupEndpointBuilder = new();

    internal GroupRouteBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern pattern)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;
        _pattern = pattern;

        if (outerEndpointRouteBuilder is GroupRouteBuilder outerGroup)
        {
            GroupPrefix = RoutePattern.Combine(outerGroup.GroupPrefix, pattern);
        }
        else
        {
            GroupPrefix = pattern;
        }

        _groupEndpointBuilder = new GroupEndpointBuilder
        {
            DisplayName = GroupPrefix.RawText
        };

        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
    }

    /// <summary>
    /// The <see cref="RoutePattern"/> prefixing all endpoints defined using this <see cref="GroupRouteBuilder"/>.
    /// This accounts for nested groups and gives the full group prefix, not just the prefix supplied to the last call to
    /// <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
    /// </summary>
    public RoutePattern GroupPrefix { get; }

    IServiceProvider IEndpointRouteBuilder.ServiceProvider => _outerEndpointRouteBuilder.ServiceProvider;
    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() => _outerEndpointRouteBuilder.CreateApplicationBuilder();
    ICollection<EndpointDataSource> IEndpointRouteBuilder.DataSources => _dataSources;
    void IEndpointConventionBuilder.Add(Action<EndpointBuilder> convention) => convention(_groupEndpointBuilder);

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
                var list = new List<Endpoint>();

                foreach (var dataSource in _groupRouteBuilder._dataSources)
                {
                    foreach (var endpoint in dataSource.Endpoints)
                    {
                        var endpointToAdd = endpoint;

                        if (endpointToAdd is RouteEndpoint routeEndpoint)
                        {
                            var combinedMetadata = _groupRouteBuilder._groupEndpointBuilder.Metadata.Concat(routeEndpoint.Metadata);

                            endpointToAdd = new RouteEndpoint(
                                // This cannot be null given a RouteEndpoint.
                                routeEndpoint.RequestDelegate!,
                                // Use _pattern instead of GroupPrefix because we could be calculating an intermediate step.
                                // Using GroupPrefix would always give the full RoutePattern and not the intermediate value.
                                RoutePattern.Combine(_groupRouteBuilder._pattern, routeEndpoint.RoutePattern),
                                routeEndpoint.Order,
                                new EndpointMetadataCollection(combinedMetadata),
                                routeEndpoint.DisplayName);
                        }

                        list.Add(endpointToAdd);
                    }
                }

                return list;
            }
        }

        public override IChangeToken GetChangeToken() => new CompositeEndpointDataSource(_groupRouteBuilder._dataSources).GetChangeToken();
    }

    private sealed class GroupEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build() => throw new NotSupportedException("A single endpoint cannot be built from a route group.");
    }
}
