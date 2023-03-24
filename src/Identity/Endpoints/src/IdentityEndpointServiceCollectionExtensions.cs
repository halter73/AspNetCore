// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default extensions to <see cref="IServiceCollection"/> for <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>.
/// </summary>
public static class IdentityEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of common identity services to the application, including a default endpoints, token providers,
    /// and configures authentication to use identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    public static IdentityBuilder AddIdentityEndpoints<TUser>(this IServiceCollection services)
        where TUser : class
        => services.AddIdentityEndpoints<TUser>(_ => { });

    /// <summary>
    /// Adds a set of common identity services to the application, including a default endpoints, token providers,
    /// and configures authentication to use identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureOptions">Configures the <see cref="IdentityOptions"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    public static IdentityBuilder AddIdentityEndpoints<TUser>(this IServiceCollection services, Action<IdentityOptions> configureOptions)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(nameof(services));
        ArgumentNullException.ThrowIfNull(nameof(configureOptions));

        services.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.BearerScheme;
        })
        .AddScheme<IdentityBearerAuthenticationOptions, IdentityBearerAuthenticationHandler>(IdentityConstants.BearerScheme, _ => { })
        .AddIdentityCookies();

        return services.AddIdentityCore<TUser>(o =>
        {
            o.Stores.MaxLengthForKeys = 128;
            configureOptions.Invoke(o);
        }).AddDefaultTokenProviders();
    }
}
