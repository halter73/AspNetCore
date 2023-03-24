// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Endpoints;

internal class IdentityBearerAuthenticationHandler : SignInAuthenticationHandler<IdentityBearerAuthenticationOptions>
{
    internal static AuthenticateResult FailedUnprotectingToken = AuthenticateResult.Fail("Unprotect token failed");
    internal static AuthenticateResult TokenExpired = AuthenticateResult.Fail("Token expired");

    private readonly IDataProtectionProvider _dataProtectionProvider;

    public IdentityBearerAuthenticationHandler(
        IOptionsMonitor<IdentityBearerAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IDataProtectionProvider dataProtectionProvider)
        : base(options, logger, encoder, clock)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cookieResult = await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (cookieResult.Succeeded)
        {
            return cookieResult;
        }

        // Otherwise check for Bearer token
        string? token = null;
        var authorization = Request.Headers.Authorization.ToString();

        // If no authorization header found, nothing to process further
        if (string.IsNullOrEmpty(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authorization["Bearer ".Length..].Trim();
        }

        // If no token found, no further work possible
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var dp = _dataProtectionProvider.CreateProtector(IdentityConstants.BearerScheme, "v1", "AccessToken");
        var ticketUnprotector = new TicketDataFormat(dp);
        var ticket = ticketUnprotector.Unprotect(token);

        if (ticket?.Properties?.ExpiresUtc is null)
        {
            return FailedUnprotectingToken;
        }

        if (Clock.UtcNow > ticket.Properties.ExpiresUtc)
        {
            return TokenExpired;
        }

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        => Context.SignInAsync(IdentityConstants.ApplicationScheme, user, properties);

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        => Context.SignOutAsync(IdentityConstants.ApplicationScheme, properties);
}
