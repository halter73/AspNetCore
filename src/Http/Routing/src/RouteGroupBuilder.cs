// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
public sealed class RouteGroupBuilder : IEndpointRouteBuilder, IEndpointConventionBuilder
{
    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;
    private readonly RoutePattern _partialPrefix;

    private readonly List<EndpointDataSource> _dataSources = new();
    private readonly List<Action<EndpointBuilder>> _conventions = new();

    internal RouteGroupBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern partialPrefix)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;
        _partialPrefix = partialPrefix;
        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
    }

    IServiceProvider IEndpointRouteBuilder.ServiceProvider => _outerEndpointRouteBuilder.ServiceProvider;
    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() => _outerEndpointRouteBuilder.CreateApplicationBuilder();
    ICollection<EndpointDataSource> IEndpointRouteBuilder.DataSources => _dataSources;
    void IEndpointConventionBuilder.Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

    // For testing
    // This accounts for nested groups and gives the full group prefix, not just the prefix supplied to the last call to MapGroup
    // If the _outerEndpointRouteBuilder is not a RouteGroupBuilder (like a wrapper around RouteGroupBuilder) this will not have
    // the final prefix used in GroupDataSource.GetGroupedEndpoints() which is why this is not public even though it seems useful.
    internal RoutePattern GroupPrefix => RoutePatternFactory.Combine((_outerEndpointRouteBuilder as RouteGroupBuilder)?.GroupPrefix, _partialPrefix);

    private sealed class GroupDataSource : EndpointDataSource
    {
        private readonly RouteGroupBuilder _routeGroupBuilder;

        public GroupDataSource(RouteGroupBuilder groupRouteBuilder)
        {
            _routeGroupBuilder = groupRouteBuilder;
        }

        public override IReadOnlyList<Endpoint> Endpoints =>
            GetGroupedEndpointsWithNullablePrefix(null, Array.Empty<Action<EndpointBuilder>>(), _routeGroupBuilder._outerEndpointRouteBuilder.ServiceProvider);

        public override IReadOnlyList<RouteEndpoint> GetGroupedEndpoints(RoutePattern prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices) =>
            GetGroupedEndpointsWithNullablePrefix(prefix, conventions, applicationServices);

        public IReadOnlyList<RouteEndpoint> GetGroupedEndpointsWithNullablePrefix(RoutePattern? prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices)
        {
            if (_routeGroupBuilder._dataSources.Count == 0)
            {
                return Array.Empty<RouteEndpoint>();
            }

            // For most EndpointDataSources, the prefix parameter is the full group prefix for the endpoint, but in the special case of the GroupDataSource
            // must add our partial prefix to this before calling into any nested EndpointDataSources. In non-nested cases where this is called from
            // GroupDataSource.Endpoints, prefix is null, so Combine won't allocate.
            var fullPrefix = RoutePatternFactory.Combine(prefix, _routeGroupBuilder._partialPrefix);

            var combinedConventions = conventions;

            // Avoid copies if this group has no conventions.
            if (_routeGroupBuilder._conventions.Count > 0)
            {
                // Or if there are no conventions passed in from the outer group.
                if (combinedConventions.Count == 0)
                {
                    combinedConventions = _routeGroupBuilder._conventions;
                }
                else
                {
                    // Apply conventions passed in from the outer group first so their metadata is added earlier in the list at a lower precedent.
                    var groupConventionsCopy = new List<Action<EndpointBuilder>>(conventions);
                    groupConventionsCopy.AddRange(_routeGroupBuilder._conventions);
                    combinedConventions = groupConventionsCopy;
                }
            }

            if (_routeGroupBuilder._dataSources.Count == 1)
            {
                return _routeGroupBuilder._dataSources[0].GetGroupedEndpoints(fullPrefix, combinedConventions, applicationServices);
            }

            var groupedEndpoints = new List<RouteEndpoint>();

            foreach (var dataSource in _routeGroupBuilder._dataSources)
            {
                groupedEndpoints.AddRange(dataSource.GetGroupedEndpoints(fullPrefix, combinedConventions, applicationServices));
            }

            return groupedEndpoints;
        }

        public override IChangeToken GetChangeToken() => new CompositeEndpointDataSource(_routeGroupBuilder._dataSources).GetChangeToken();
    }
}
