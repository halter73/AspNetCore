// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing;

internal sealed class RouteHandlerEndpointDataSource : EndpointDataSource
{
    private readonly List<RouteHandlerEndpointDataSourceEntry> _routeHandlerContexts = new();
    private readonly IServiceProvider _applicationServices;
    private readonly bool _throwOnBadRequest;

    public RouteHandlerEndpointDataSource(IServiceProvider applicationServices, bool throwOnBadRequest)
    {
        _applicationServices = applicationServices;
        _throwOnBadRequest = throwOnBadRequest;
    }

    public void AddEndpoint(
        RouteEndpointBuilder builder,
        Delegate routeHandler,
        IEnumerable<object>? initialEndpointMetadata,
        bool disableInferFromBodyParameters)
    {
        _routeHandlerContexts.Add(new RouteHandlerEndpointDataSourceEntry
        {
            RouteEndpointBuilder = builder,
            RouteHandler = routeHandler,
            InitialEndpointMetadata = initialEndpointMetadata,
            DisableInferFromBodyParameters = disableInferFromBodyParameters,
        });
    }

    [UnconditionalSuppressMessage("Trimmer", "IL2026",
        Justification = "We surface a RequireUnreferencedCode in the call to Map method adding this EndpointDataSource. " +
                        "The trimmer is unable to infer this.")]
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            var endpoints = new List<RouteEndpoint>(_routeHandlerContexts.Count);

            foreach (var context in _routeHandlerContexts)
            {
                var builder = context.RouteEndpointBuilder;
                var routeParams = builder.RoutePattern.Parameters;
                var routeParamNames = new List<string>(routeParams.Count);
                foreach (var parameter in routeParams)
                {
                    routeParamNames.Add(parameter.Name);
                }

                List<Func<RouteHandlerContext, RouteHandlerFilterDelegate, RouteHandlerFilterDelegate>>? routeHandlerFilterFactories = null;

                foreach (var item in builder.Metadata)
                {
                    if (item is Func<RouteHandlerContext, RouteHandlerFilterDelegate, RouteHandlerFilterDelegate> filter)
                    {
                        routeHandlerFilterFactories ??= new();
                        routeHandlerFilterFactories.Add(filter);
                    }
                }

                var factoryOptions = new RequestDelegateFactoryOptions
                {
                    ServiceProvider = _applicationServices,
                    RouteParameterNames = routeParamNames,
                    ThrowOnBadRequest = _throwOnBadRequest,
                    DisableInferBodyFromParameters = context.DisableInferFromBodyParameters,
                    RouteHandlerFilterFactories = routeHandlerFilterFactories,
                    InitialEndpointMetadata = context.InitialEndpointMetadata,
                };
                var filteredRequestDelegateResult = RequestDelegateFactory.Create(context.RouteHandler, factoryOptions);

                // Add request delegate metadata
                foreach (var metadata in filteredRequestDelegateResult.EndpointMetadata)
                {
                    builder.Metadata.Add(metadata);
                }

                builder.RequestDelegate = filteredRequestDelegateResult.RequestDelegate;
                endpoints.Add((RouteEndpoint)builder.Build());
            }

            return endpoints;
        }
    }

    public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    private struct RouteHandlerEndpointDataSourceEntry
    {
        public RouteEndpointBuilder RouteEndpointBuilder { get; init; }
        public Delegate RouteHandler { get; init; }
        public IEnumerable<object>? InitialEndpointMetadata { get; init; }
        public bool DisableInferFromBodyParameters { get; init; }
    }
}
