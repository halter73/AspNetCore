// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication.BearerToken.DTO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Authentication.BearerToken;

internal sealed class BearerTokenHandler(
    IOptionsMonitor<BearerTokenOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    UrlEncoder urlEncoder,
    IDataProtectionProvider dataProtectionProvider)
    : SignInAuthenticationHandler<BearerTokenOptions>(optionsMonitor, loggerFactory, urlEncoder)
{
    private const string BearerTokenPurpose = "BearerToken";
    private const string RefreshTokenPurpose = "RefreshToken";

    private static readonly AuthenticateResult FailedUnprotectingToken = AuthenticateResult.Fail("Unprotected token failed");
    private static readonly AuthenticateResult TokenExpired = AuthenticateResult.Fail("Token expired");

    private ISecureDataFormat<AuthenticationTicket> TokenProtector
        => Options.TokenProtector ?? new TicketDataFormat(dataProtectionProvider.CreateProtector("Microsoft.AspNetCore.Authentication.BearerToken"));

    private new BearerTokenEvents Events => (BearerTokenEvents)base.Events!;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Give application opportunity to find from a different location, adjust, or reject token
        var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options);

        await Events.MessageReceived(messageReceivedContext);

        if (messageReceivedContext.Result != null)
        {
            return messageReceivedContext.Result;
        }

        var token = messageReceivedContext.Token ?? GetBearerTokenOrNull();

        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var ticket = TokenProtector.Unprotect(token, BearerTokenPurpose);

        if (ticket?.Properties?.ExpiresUtc is null)
        {
            return FailedUnprotectingToken;
        }

        if (TimeProvider.GetUtcNow() >= ticket.Properties.ExpiresUtc)
        {
            return TokenExpired;
        }

        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        await base.HandleChallengeAsync(properties);
    }

    protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        long expiresInTotalSeconds;
        var utcNow = TimeProvider.GetUtcNow();

        properties ??= new();

        if (properties.ExpiresUtc is null)
        {
            properties.ExpiresUtc ??= utcNow + Options.BearerTokenExpiration;
            expiresInTotalSeconds = (long)Options.BearerTokenExpiration.TotalSeconds;
        }
        else
        {
            expiresInTotalSeconds = (long)(properties.ExpiresUtc.Value - utcNow).TotalSeconds;
        }

        var accessTicket = new AuthenticationTicket(user, properties, Scheme.Name);
        var accessTokenResponse = new AccessTokenResponse
        {
            AccessToken = TokenProtector.Protect(accessTicket, BearerTokenPurpose),
            ExpiresInTotalSeconds = expiresInTotalSeconds,
            RefreshToken = TokenProtector.Protect(CreateRefreshTicket(user, utcNow), RefreshTokenPurpose),
        };

        return Context.Response.WriteAsJsonAsync(accessTokenResponse, BearerTokenJsonSerializerContext.Default.AccessTokenResponse);
    }

    private AuthenticationTicket CreateRefreshTicket(ClaimsPrincipal user, DateTimeOffset utcNow)
    {
        var refreshProperties = new AuthenticationProperties
        {
            ExpiresUtc = utcNow + Options.RefreshTokenExpiration
        };

        var refreshPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            user.FindFirst(ClaimTypes.NameIdentifier) ?? throw new ArgumentException(null, nameof(user)),
            // TODO: Use ClaimsIdentityOptions.SecurityStampClaimType
            user.FindFirst("AspNet.Identity.SecurityStamp") ?? throw new ArgumentException(null, nameof(user)),
        }));

        return new AuthenticationTicket(refreshPrincipal, refreshProperties, $"{Scheme.Name}:{RefreshTokenPurpose}");
    }

    // No-op to avoid interfering with any mass sign-out logic.
    protected override Task HandleSignOutAsync(AuthenticationProperties? properties) => Task.CompletedTask;

    private string? GetBearerTokenOrNull()
    {
        var authorization = Request.Headers.Authorization.ToString();

        return authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            ? authorization["Bearer ".Length..]
            : null;
    }
}
