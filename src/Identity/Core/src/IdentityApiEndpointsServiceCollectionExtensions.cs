// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default extensions to <see cref="IServiceCollection"/> for <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentityApi{TUser}(IEndpointRouteBuilder)"/>.
/// </summary>
public static class IdentityApiEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentityApi{TUser}(IEndpointRouteBuilder)"/>
    /// and configures authentication to support identity bearer tokens and cookies.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IdentityBuilder"/>.</returns>
    [RequiresUnreferencedCode("Authentication middleware does not currently support native AOT.", Url = "https://aka.ms/aspnet/nativeaot")]
    public static IdentityBuilder AddIdentityApiEndpoints<TUser>(this IServiceCollection services)
        where TUser : class, new()
        => services.AddIdentityApiEndpoints<TUser>(_ => { });

    /// <summary>
    /// Adds a set of common identity services to the application to support <see cref="IdentityApiEndpointRouteBuilderExtensions.MapIdentityApi{TUser}(IEndpointRouteBuilder)"/>
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

        services
            .AddAuthentication(IdentityConstants.BearerAndApplicationScheme)
            .AddScheme<AuthenticationSchemeOptions, CompositeIdentityHandler>(IdentityConstants.BearerAndApplicationScheme, null, compositeOptions =>
            {
                compositeOptions.ForwardDefault = IdentityConstants.BearerScheme;
                compositeOptions.ForwardAuthenticate = IdentityConstants.BearerAndApplicationScheme;
            })
            .AddBearerToken(IdentityConstants.BearerScheme, bearerOptions =>
            {
                bearerOptions.Events.OnSigningIn = HandleSigningIn<TUser>;
            })
            .AddIdentityCookies();

        return services.AddIdentityCore<TUser>(o =>
            {
                o.Stores.MaxLengthForKeys = 128;
                configure(o);
            })
            .AddApiEndpoints();
    }

    private static async Task HandleSigningIn<TUser>(SigningInContext signInContext)
        where TUser : class, new()
    {
        if (signInContext.Properties.RefreshToken is null)
        {
            // Only validate the security stamp and refresh the user from the store during /refresh
            // not during the initial /login when the Principal is already newly created from the store.
            return;
        }

        var signInManager = signInContext.HttpContext.RequestServices.GetRequiredService<SignInManager<TUser>>();

        if (await signInManager.ValidateSecurityStampAsync(signInContext.Principal) is not TUser user)
        {
            // Reject the /refresh attempt if the security stamp validation fails which will result in a 401 challenge.
            signInContext.Principal = null;
            return;
        }

        signInContext.Principal = await signInManager.CreateUserPrincipalAsync(user);
    }

    private sealed class CompositeIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : SignInAuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var bearerResult = await Context.AuthenticateAsync(IdentityConstants.BearerScheme);

            // Only try to authenticate with the application cookie if there is no bearer token.
            if (!bearerResult.None)
            {
                return bearerResult;
            }

            // Cookie auth will return AuthenticateResult.NoResult() like bearer auth just did if there is no cookie.
            return await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        }

        protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        {
            throw new NotImplementedException();
        }

        protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        {
            throw new NotImplementedException();
        }
    }
}
