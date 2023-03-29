// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default extensions to <see cref="IServiceCollection"/> for <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>.
/// </summary>
public static class IdentityEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>
    /// and configures authentication to support identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    public static IdentityBuilder AddIdentityEndpoints<TUser>(this IServiceCollection services)
        where TUser : class, new()
        => services.AddIdentityEndpoints<TUser>(_ => { });

    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>
    /// and configures authentication to support identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureIdentityOptions">Configures the <see cref="IdentityOptions"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    public static IdentityBuilder AddIdentityEndpoints<TUser>(this IServiceCollection services, Action<IdentityOptions> configureIdentityOptions)
        where TUser : class, new()
    {
        services.AddAuthentication(o => o.DefaultScheme = IdentityConstants.BearerScheme)
            .AddIdentityBearer(options =>
            {
                options.BearerTokenMissingFallbackScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();

        return services.AddIdentityEndpointsCore<TUser>(configureIdentityOptions)
            .AddDefaultTokenProviders()
            .AddSignInManager();
    }

    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>
    /// but does not configure authentication. Call <see cref="IdentityBearerAuthenticationBuilderExtensions.AddIdentityBearer(AuthenticationBuilder, Action{IdentityBearerOptions}?)"/> and/or
    /// <see cref="IdentityCookieAuthenticationBuilderExtensions.AddIdentityCookies(AuthenticationBuilder)"/> to configure authentication separately.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureIdentityOptions">Configures the <see cref="IdentityOptions"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    public static IdentityBuilder AddIdentityEndpointsCore<TUser>(
        this IServiceCollection services,
        Action<IdentityOptions> configureIdentityOptions)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(nameof(services));
        ArgumentNullException.ThrowIfNull(nameof(configureIdentityOptions));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<JsonOptions>, IdentityEndpointJsonOptionsSetup>());

        var identityBuilder = services.AddIdentityCore<TUser>(o =>
        {
            o.Stores.MaxLengthForKeys = 128;
            configureIdentityOptions(o);
        });

        return identityBuilder;
    }
}
