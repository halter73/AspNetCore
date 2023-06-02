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
        => Options.TokenProtector ?? new TicketDataFormat(dataProtectionProvider.CreateProtector("Microsoft.AspNetCore.Authentication.BearerToken", Scheme.Name));

    private new BearerTokenEvents Events => (BearerTokenEvents)base.Events!;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Give application opportunity to find from a different location, adjust, or reject token
        var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options);

        await Events.MessageReceivedAsync(messageReceivedContext);

        if (messageReceivedContext.Result is not null)
        {
            return messageReceivedContext.Result;
        }

        var token = messageReceivedContext.Token ?? GetBearerTokenOrNull();

        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var ticket = TokenProtector.Unprotect(token, BearerTokenPurpose);

        if (ticket?.Properties?.ExpiresUtc is not { } expiration)
        {
            return FailedUnprotectingToken;
        }

        if (TimeProvider.GetUtcNow() >= expiration)
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

    protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        properties ??= new();

        var utcNow = TimeProvider.GetUtcNow();
        properties.ExpiresUtc ??= utcNow + Options.BearerTokenExpiration;

        var signingInContext = new SigningInContext(
            Context,
            Scheme,
            Options,
            user,
            properties);

        await Events.SigningInAsync(signingInContext);

        var response = new AccessTokenResponse
        {
            AccessToken = signingInContext.AccessToken
                ?? TokenProtector.Protect(new AuthenticationTicket(user, properties, Scheme.Name), BearerTokenPurpose),
            ExpiresInTotalSeconds = (long)Math.Round(properties.ExpiresUtc switch
            {
                DateTimeOffset expiration => (expiration - utcNow).TotalSeconds,
                _ => Options.BearerTokenExpiration.TotalSeconds,
            }),
            RefreshToken = signingInContext.RefreshToken
                ?? TokenProtector.Protect(CreateRefreshTicket(user, utcNow), RefreshTokenPurpose),
        };

        await Context.Response.WriteAsJsonAsync(response, BearerTokenJsonSerializerContext.Default.AccessTokenResponse);
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

        return new AuthenticationTicket(user, refreshProperties, $"{Scheme.Name}:{RefreshTokenPurpose}");
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
