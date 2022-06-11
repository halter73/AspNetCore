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

    private readonly List<EndpointDataSource> _dataSources = new();
    private readonly List<Action<EndpointBuilder>> _conventions = new();

    internal RouteGroupBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern partialPrefix)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;

        if (outerEndpointRouteBuilder is RouteGroupBuilder outerGroup)
        {
            GroupPrefix = RoutePatternFactory.Combine(outerGroup.GroupPrefix, partialPrefix);
        }
        else
        {
            GroupPrefix = partialPrefix;
        }

        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
    }

    /// <summary>
    /// The <see cref="RoutePattern"/> prefixing all endpoints defined using this <see cref="RouteGroupBuilder"/>.
    /// This accounts for nested groups and gives the full group prefix, not just the prefix supplied to the last call to
    /// <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
    /// </summary>
    public RoutePattern GroupPrefix { get; }

    IServiceProvider IEndpointRouteBuilder.ServiceProvider => _outerEndpointRouteBuilder.ServiceProvider;
    IApplicationBuilder IEndpointRouteBuilder.CreateApplicationBuilder() => _outerEndpointRouteBuilder.CreateApplicationBuilder();
    ICollection<EndpointDataSource> IEndpointRouteBuilder.DataSources => _dataSources;
    void IEndpointConventionBuilder.Add(Action<EndpointBuilder> convention) => _conventions.Add(convention);

    // REVIEW: Should this be public or is being able to use this via the default implementation of EndpointDataSource.GetGroupEndpoints() enough.
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
            wrappedEndpoints.Add(new RouteEndpoint(
                // Again, RequestDelegate can never be null given a RouteEndpoint.
                routeEndpointBuilder.RequestDelegate!,
                fullRoutePattern,
                routeEndpointBuilder.Order,
                new(routeEndpointBuilder.Metadata),
                routeEndpointBuilder.DisplayName));
        }

        return wrappedEndpoints;
    }

    private sealed class GroupDataSource : EndpointDataSource
    {
        private readonly RouteGroupBuilder _groupRouteBuilder;

        public GroupDataSource(RouteGroupBuilder groupRouteBuilder)
        {
            _groupRouteBuilder = groupRouteBuilder;
        }

        public override IReadOnlyList<Endpoint> Endpoints => GetGroupedEndpoints(
            _groupRouteBuilder.GroupPrefix,
            Array.Empty<Action<EndpointBuilder>>(),
            _groupRouteBuilder._outerEndpointRouteBuilder.ServiceProvider);

        public override IReadOnlyList<Endpoint> GetGroupedEndpoints(RoutePattern prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices)
        {
            if (_groupRouteBuilder._dataSources.Count == 0)
            {
                return Array.Empty<Endpoint>();
            }

            var combinedConventions = conventions;

            if (_groupRouteBuilder._conventions.Count > 0)
            {
                if (combinedConventions.Count == 0)
                {
                    combinedConventions = _groupRouteBuilder._conventions;
                }
                else
                {
                    var groupConventionsCopy = new List<Action<EndpointBuilder>>(_groupRouteBuilder._conventions);
                    groupConventionsCopy.AddRange(conventions);
                    combinedConventions = groupConventionsCopy;
                }
            }

            if (_groupRouteBuilder._dataSources.Count == 1)
            {
                return _groupRouteBuilder._dataSources[0].GetGroupedEndpoints(prefix, combinedConventions, applicationServices);
            }

            var groupedEndpoints = new List<Endpoint>();

            foreach (var dataSource in _groupRouteBuilder._dataSources)
            {
                groupedEndpoints.AddRange(dataSource.GetGroupedEndpoints(prefix, combinedConventions, applicationServices));
            }

            return groupedEndpoints;
        }

        public override IChangeToken GetChangeToken() => new CompositeEndpointDataSource(_groupRouteBuilder._dataSources).GetChangeToken();
    }
}
