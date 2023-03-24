// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Identity.Endpoints;

internal sealed class IdentityBearerAuthenticationHandler : SignInAuthenticationHandler<IdentityBearerAuthenticationOptions>
{
    internal static Task<AuthenticateResult> FailedUnprotectingTokenTask = Task.FromResult(AuthenticateResult.Fail("Unprotect token failed"));
    internal static Task<AuthenticateResult> TokenExpiredTask = Task.FromResult(AuthenticateResult.Fail("Token expired"));

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private TicketDataFormat? _accessTokenProtector;

    public IdentityBearerAuthenticationHandler(
        IOptionsMonitor<IdentityBearerAuthenticationOptions> optionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder urlEncoder,
        ISystemClock clock,
        IDataProtectionProvider dataProtectionProvider)
        : base(optionsMonitor, loggerFactory, urlEncoder, clock)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    private TicketDataFormat AccessTokenProtector =>
        _accessTokenProtector ??= new(_dataProtectionProvider.CreateProtector(IdentityConstants.BearerScheme, "v1", "AccessToken"));

    private TimeSpan AccessTokenExpiration => OptionsMonitor.Get(IdentityConstants.BearerScheme).AccessTokenExpiration;

    internal string CreateAccessToken(ClaimsPrincipal principal)
    {
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = Clock.UtcNow + AccessTokenExpiration,
        };

        var ticket = new AuthenticationTicket(principal, properties, IdentityConstants.BearerScheme);
        return AccessTokenProtector.Protect(ticket);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // If there's no bearer token, forward to cookie auth.
        if (GetBearerTokenOrNull() is not string token)
        {
            return Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        }

        var ticket = AccessTokenProtector.Unprotect(token);

        if (ticket?.Properties?.ExpiresUtc is null)
        {
            return FailedUnprotectingTokenTask;
        }

        if (Clock.UtcNow > ticket.Properties.ExpiresUtc)
        {
            return TokenExpiredTask;
        }

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (GetBearerTokenOrNull() is null)
        {
            return Context.ChallengeAsync(IdentityConstants.ApplicationScheme);
        }

        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        return base.HandleChallengeAsync(properties);
    }

    // Forward to cookie auth.
    protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        => Context.SignInAsync(IdentityConstants.ApplicationScheme, user, properties);

    // Delete cookies and clear session if any. This does not prevent a bad client from reusing the bearer token or cookie.
    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        => Context.SignOutAsync(IdentityConstants.ApplicationScheme, properties);

    private string? GetBearerTokenOrNull()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return null;
        }

        return authorization["Bearer ".Length..];
    }
}
