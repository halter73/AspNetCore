// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Endpoints.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to configure an <see cref="IServiceCollection"/> for WebAssembly components.
/// </summary>
public static class WebAssemblyRazorComponentsBuilderExtensions
{
    /// <summary>
    /// Adds services to support rendering interactive WebAssembly components.
    /// </summary>
    /// <param name="builder">The <see cref="IRazorComponentsBuilder"/>.</param>
    /// <returns>An <see cref="IRazorComponentsBuilder"/> that can be used to further customize the configuration.</returns>
    public static IRazorComponentsBuilder AddInteractiveWebAssemblyComponents(this IRazorComponentsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<RenderModeEndpointProvider, WebAssemblyEndpointProvider>());

        return builder;
    }

    /// <summary>
    /// Serializes the <see cref="AuthenticationState"/> returned by the server-side <see cref="AuthenticationStateProvider"/> using <see cref="PersistentComponentState"/>
    /// for use by interactive WebAssembly components via a deserializing client-side <see cref="AuthenticationStateProvider"/> which can be added by calling
    /// AddAuthenticationStateDeserialization from the Microsoft.AspNetCore.Components.WebAssembly.Authentication package in the client project.
    /// </summary>
    /// <param name="builder">The <see cref="IRazorComponentsBuilder"/>.</param>
    /// <param name="configure">A callback to customize the serialization of the <see cref="AuthenticationState"/>.</param>
    /// <returns>An <see cref="IRazorComponentsBuilder"/> that can be used to further customize the configuration.</returns>
    public static IRazorComponentsBuilder AddAuthenticationStateSerialization(this IRazorComponentsBuilder builder, Action<AuthenticationStateSerializationOptions>? configure = null)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IHostEnvironmentAuthenticationStateProvider, AuthenticationStateSerializer>());
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        return builder;
    }

    private class WebAssemblyEndpointProvider(IServiceProvider services) : RenderModeEndpointProvider
    {
        public override IEnumerable<RouteEndpointBuilder> GetEndpointBuilders(IComponentRenderMode renderMode, IApplicationBuilder applicationBuilder)
        {
            if (renderMode is not WebAssemblyRenderModeWithOptions wasmWithOptions)
            {
                return renderMode is InteractiveWebAssemblyRenderMode
                    ? throw new InvalidOperationException("Invalid render mode. Use AddInteractiveWebAssemblyRenderMode(Action<WebAssemblyComponentsEndpointOptions>) to configure the WebAssembly render mode.")
                    : (IEnumerable<RouteEndpointBuilder>)Array.Empty<RouteEndpointBuilder>();
            }
            if (wasmWithOptions is { EndpointOptions.ConventionsApplied: true })
            {
                return []; // No need to add additional endpoints to the DS, they are already added
            }
            else
            {
                // In case the app didn't call MapStaticAssets, use the 8.0 approach to map the assets.
                var endpointRouteBuilder = new EndpointRouteBuilder(services, applicationBuilder);
                var pathPrefix = wasmWithOptions.EndpointOptions?.PathPrefix;

                applicationBuilder.UseBlazorFrameworkFiles(pathPrefix ?? default);
                var app = applicationBuilder.Build();

                endpointRouteBuilder.Map($"{pathPrefix}/_framework/{{*path}}", context =>
                {
                    // Set endpoint to null so the static files middleware will handle the request.
                    context.SetEndpoint(null);

                    return app(context);
                });

                return endpointRouteBuilder.GetEndpoints();
            }
        }

        public override bool Supports(IComponentRenderMode renderMode) =>
            renderMode is InteractiveWebAssemblyRenderMode or InteractiveAutoRenderMode;

        private class EndpointRouteBuilder : IEndpointRouteBuilder
        {
            private readonly IApplicationBuilder _applicationBuilder;

            public EndpointRouteBuilder(IServiceProvider serviceProvider, IApplicationBuilder applicationBuilder)
            {
                ServiceProvider = serviceProvider;
                _applicationBuilder = applicationBuilder;
            }

            public IServiceProvider ServiceProvider { get; }

            public ICollection<EndpointDataSource> DataSources { get; } = [];

            public IApplicationBuilder CreateApplicationBuilder()
            {
                return _applicationBuilder.New();
            }

            internal IEnumerable<RouteEndpointBuilder> GetEndpoints()
            {
                foreach (var ds in DataSources)
                {
                    foreach (var endpoint in ds.Endpoints)
                    {
                        var routeEndpoint = (RouteEndpoint)endpoint;
                        var builder = new RouteEndpointBuilder(endpoint.RequestDelegate, routeEndpoint.RoutePattern, routeEndpoint.Order);
                        for (var i = 0; i < routeEndpoint.Metadata.Count; i++)
                        {
                            var metadata = routeEndpoint.Metadata[i];
                            builder.Metadata.Add(metadata);
                        }

                        yield return builder;
                    }
                }
            }
        }
    }
}
