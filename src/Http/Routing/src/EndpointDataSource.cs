// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides a collection of <see cref="Endpoint"/> instances.
/// </summary>
public abstract class EndpointDataSource
{
    /// <summary>
    /// Gets a <see cref="IChangeToken"/> used to signal invalidation of cached <see cref="Endpoint"/>
    /// instances.
    /// </summary>
    /// <returns>The <see cref="IChangeToken"/>.</returns>
    public abstract IChangeToken GetChangeToken();

    /// <summary>
    /// Returns a read-only collection of <see cref="Endpoint"/> instances.
    /// </summary>
    public abstract IReadOnlyList<Endpoint> Endpoints { get; }

    /// <summary>
    /// Get the <see cref="Endpoint"/> instances for this <see cref="EndpointDataSource"/> given the specified group
    /// <paramref name="prefix"/> and <paramref name="conventions"/>.
    /// </summary>
    /// <param name="prefix">
    /// The prefix the precedes. This accounts for nested groups and gives the full group prefix,
    /// not just the prefix supplied to the last call to <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
    /// </param>
    /// <param name="conventions">
    /// Any convention added to the <see cref="RouteGroupBuilder"/> via <see cref="IEndpointConventionBuilder.Add(Action{EndpointBuilder})"/>.
    /// </param>
    /// <param name="applicationServices">
    /// Gets the <see cref="IServiceProvider"/> instance used to access application services.
    /// </param>
    /// <returns>
    /// Returns a read-only collection of <see cref="Endpoint"/> instances given the specified group <paramref name="prefix"/> and <paramref name="conventions"/>.
    /// </returns>
    public virtual IReadOnlyList<RouteEndpoint> GetGroupedEndpoints(RoutePattern prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices)
    {
        var wrappedEndpoints = new List<RouteEndpoint>();

        foreach (var endpoint in Endpoints)
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
            wrappedEndpoints.Add((RouteEndpoint)routeEndpointBuilder.Build());
        }

        return wrappedEndpoints;
    }
}
