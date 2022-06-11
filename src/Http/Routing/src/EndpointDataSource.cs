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
    /// Get the <see cref="Endpoint"/> instances for this <see cref="EndpointDataSource"/> given the specified group <paramref name="prefix"/> and <paramref name="conventions"/>.
    /// </summary>
    /// <param name="prefix">
    /// The <see cref="RouteGroupBuilder.GroupPrefix"/>. This accounts for nested groups and gives the full group prefix, not just the prefix supplied to the last call to
    /// <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
    /// </param>
    /// <param name="conventions">Any convention added to the <see cref="RouteGroupBuilder"/> via <see cref="IEndpointConventionBuilder.Add(Action{EndpointBuilder})"/>.</param>
    /// <param name="applicationServices">Gets the <see cref="IServiceProvider"/> instance used to access application services.</param>
    /// <returns>Returns a read-only collection of <see cref="Endpoint"/> instances given the specified group <paramref name="prefix"/> and <paramref name="conventions"/>.</returns>
    public virtual IReadOnlyList<Endpoint> GetGroupedEndpoints(RoutePattern prefix, IReadOnlyList<Action<EndpointBuilder>> conventions, IServiceProvider applicationServices) =>
        RouteGroupBuilder.WrapGroupEndpoints(prefix, conventions, applicationServices, Endpoints);
}
