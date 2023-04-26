// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default extensions to <see cref="IServiceCollection"/> for <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>.
/// </summary>
public static class IdentityApiEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>
    /// and configures authentication to support identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    [RequiresUnreferencedCode("Authentication middleware does not currently support native AOT.", Url = "https://aka.ms/aspnet/nativeaot")]
    public static IdentityBuilder AddIdentityApiEndpoints<TUser>(this IServiceCollection services)
        where TUser : class, new()
        => services.AddIdentityApiEndpoints<TUser>(_ => { });

    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>
    /// and configures authentication to support identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Configures the <see cref="IdentityOptions"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    [RequiresUnreferencedCode("Authentication middleware does not currently support native AOT.", Url = "https://aka.ms/aspnet/nativeaot")]
    public static IdentityBuilder AddIdentityApiEndpoints<TUser>(this IServiceCollection services, Action<IdentityOptions> configure)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme, o =>
            {
                o.MissingBearerTokenFallbackScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();

        return services.AddIdentityCore<TUser>(o =>
            {
                o.Stores.MaxLengthForKeys = 128;
                configure(o);
            })
            .AddApiEndpoints();
    }
}
