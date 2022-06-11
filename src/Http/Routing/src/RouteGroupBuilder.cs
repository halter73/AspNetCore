// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
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
    private static readonly RoutePattern _emptyPrefix = new RoutePattern(
        rawText: null,
        defaults: ImmutableDictionary<string, object?>.Empty,
        parameterPolicies: ImmutableDictionary<string, IReadOnlyList<RoutePatternParameterPolicyReference>>.Empty,
        requiredValues: ImmutableDictionary<string, object?>.Empty,
        parameters: Array.Empty<RoutePatternParameterPart>(),
        pathSegments: Array.Empty<RoutePatternPathSegment>());

    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;
    private readonly RoutePattern _partialPrefix;
    private readonly RoutePattern? _parentPrefix;

    private readonly List<EndpointDataSource> _dataSources = new();
    private readonly List<Action<EndpointBuilder>> _conventions = new();

    internal RouteGroupBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern partialPrefix)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;
        _partialPrefix = partialPrefix;

        // You can still nest groups with a custom outerEndpointRouteBuilder (say a wrapper) even though RouteGroupBuilder is sealed and
        // we read the internal GroupPrefix here. If outerEndpointRouteBuilder.GroupPrefix happens to match the prefix argument to our
        // EndpointDataSource.GetGroupedEndpoints() implementation we avoid some allocations. If not, no big deal.
        // It does mean that GroupPrefix property on this RouteGroupBuilder will not count the outer prefix though.
        if (outerEndpointRouteBuilder is RouteGroupBuilder outerGroup)
        {
            _parentPrefix = outerGroup.GroupPrefix;
            GroupPrefix = RoutePatternFactory.Combine(_parentPrefix, partialPrefix);
        }
        else
        {
            _parentPrefix = _emptyPrefix;
            GroupPrefix = partialPrefix;
        }

        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
    }

    /// <summary>
    /// The <see cref="RoutePattern"/> prefixing all endpoints defined using this <see cref="RouteGroupBuilder"/>.
    /// This accounts for nested groups and gives the full group prefix, not just the prefix supplied to the last call to
    /// <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
    /// </summary>
    internal RoutePattern GroupPrefix => RoutePatternFactory.Combine(_parentPrefix, _partialPrefix);

    IServiceProvider IEndpointRouteBuilder.ServiceProvider => _outerEndpointRouteBuilder.ServiceProvider;
    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() => _outerEndpointRouteBuilder.CreateApplicationBuilder();
    ICollection<EndpointDataSource> IEndpointRouteBuilder.DataSources => _dataSources;
    void IEndpointConventionBuilder.Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

    internal static IReadOnlyList<Endpoint> WrapGroupEndpoints(
        RoutePattern prefix,
        IEnumerable<Action<EndpointBuilder>> conventions,
        IServiceProvider? applicationServices,
        IReadOnlyList<Endpoint> innerEndpoints)
    {
        var wrappedEndpoints = new List<Endpoint>();

        foreach (var endpoint in innerEndpoints)
        {
            // Endpoint does not provide a RoutePattern but RouteEndpoint does. So it's impossible to apply a prefix for custom Endpoints.
            // Supporting arbitrary Endpoints just to add group metadata would require changing the Endpoint type breaking any real scenario.
            if (endpoint is not RouteEndpoint routeEndpoint)
            {
                throw new NotSupportedException(Resources.FormatMapGroup_CustomEndpointUnsupported(endpoint.GetType()));
            }

            // Make the full route pattern visible to IEndpointConventionBuilder extension methods called on the group.
            // This includes patterns from any parent groups.
            var fullRoutePattern = RoutePatternFactory.Combine(prefix, routeEndpoint.RoutePattern);

            // RequestDelegate can never be null on a RouteEndpoint. The nullability carries over from Endpoint.
            var routeEndpointBuilder = new RouteEndpointBuilder(routeEndpoint.RequestDelegate!, fullRoutePattern, routeEndpoint.Order)
            {
                DisplayName = routeEndpoint.DisplayName,
                ServiceProvider = applicationServices,
            };

            // Apply group conventions to each endpoint in the group at a lower precedent than metadata already on the endpoint.
            foreach (var convention in conventions)
            {
                convention(routeEndpointBuilder);
            }

            // Any metadata already on the RouteEndpoint must have been applied directly to the endpoint or to a nested group.
            // This makes the metadata more specific than what's being applied to this group. So add it after this group's conventions.
            foreach (var metadata in routeEndpoint.Metadata)
            {
                routeEndpointBuilder.Metadata.Add(metadata);
            }

            // The RequestDelegate, Order and DisplayName can all be overridden by non-group-aware conventions. Unlike with metadata,
            // if a convention is applied to a group that changes any of these, I would expect these to be overridden as there's no
            // reasonable way to merge these properties.
            wrappedEndpoints.Add(routeEndpointBuilder.Build());
        }

        return wrappedEndpoints;
    }

    private sealed class GroupDataSource : EndpointDataSource
    {
        private readonly RouteGroupBuilder _routeGroupBuilder;

        public GroupDataSource(RouteGroupBuilder groupRouteBuilder)
        {
            _routeGroupBuilder = groupRouteBuilder;
        }

        public override IReadOnlyList<Endpoint> Endpoints => GetGroupedEndpoints(
            _routeGroupBuilder._parentPrefix,
            Array.Empty<Action<EndpointBuilder>>(),
            _routeGroupBuilder._outerEndpointRouteBuilder.ServiceProvider);

        public override IReadOnlyList<Endpoint> GetGroupedEndpoints(RoutePattern prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices)
        {
            if (_routeGroupBuilder._dataSources.Count == 0)
            {
                return Array.Empty<Endpoint>();
            }

            // For most EndpointDataSources, prefix is the full prefix for the endpoint, but in the special case of the GroupDataSource we have to add our
            // partial prefix to this before calling into any nested EndpointDataSources. In most cases where this is called from GroupDataSource.Endpoints
            // or the outerEndpointRouteBuilder is a RouteGroupBuilder, we've already calculated this GroupPrefix.
            var fullPrefix = _routeGroupBuilder._partialPrefix;
            if (!ReferenceEquals(prefix, _emptyPrefix))
            {
                fullPrefix = RoutePatternFactory.Combine(prefix, _routeGroupBuilder._partialPrefix);
            }

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

            var groupedEndpoints = new List<Endpoint>();

            foreach (var dataSource in _routeGroupBuilder._dataSources)
            {
                groupedEndpoints.AddRange(dataSource.GetGroupedEndpoints(fullPrefix, combinedConventions, applicationServices));
            }

            return groupedEndpoints;
        }

        public override IChangeToken GetChangeToken() => new CompositeEndpointDataSource(_routeGroupBuilder._dataSources).GetChangeToken();
    }
}
