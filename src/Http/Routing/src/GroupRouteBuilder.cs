// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// A builder that implements both a <see cref="IEndpointRouteBuilder"/> and a <see cref="IEndpointConventionBuilder"/>.
/// It can be used to add endpoints with the given <see cref="GroupPrefix"/>, and to customize those endpoints using conventions.
/// </summary>

public sealed class GroupRouteBuilder : IEndpointRouteBuilder, IEndpointConventionBuilder
{
    private readonly IEndpointRouteBuilder _outerEndpointRouteBuilder;
    private readonly RoutePattern _pattern;

    private readonly List<Action<EndpointBuilder>> _conventions = new();

    internal GroupRouteBuilder(IEndpointRouteBuilder outerEndpointRouteBuilder, RoutePattern pattern)
    {
        _outerEndpointRouteBuilder = outerEndpointRouteBuilder;
        _outerEndpointRouteBuilder.DataSources.Add(new GroupDataSource(this));
        _pattern = pattern;

        if (outerEndpointRouteBuilder is GroupRouteBuilder outerGroup)
        {
            GroupPrefix = RoutePattern.Combine(outerGroup.GroupPrefix, pattern);
        }
        else
        {
            GroupPrefix = pattern;
        }
    }

    /// <summary>
    /// The <see cref="RoutePattern"/> prefixing all endpoints defined using this <see cref="GroupRouteBuilder"/>.
    /// This accounts nested groups and gives the full group prefix, not just the prefix supplied to the last call to
    /// <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, RoutePattern)"/>.
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
                                // Use _pattern instead of GroupPrefix because we could be calculating an intermediate step.
                                // Using GroupPrefix would always give the full RoutePattern and not the intermediate value.
                                RoutePattern.Combine(_groupRouteBuilder._pattern, routeEndpoint.RoutePattern),
                                routeEndpoint.Order,
                                new EndpointMetadataCollection(routeEndpoint.Metadata.Concat(groupEndpointBuilder.Metadata)),
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
