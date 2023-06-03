// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Authentication.BearerToken;

/// <summary>
/// A context for <see cref="BearerTokenEvents.OnSigningIn"/>.
/// </summary>
public class SigningInContext : PrincipalContext<BearerTokenOptions>
{
    /// <summary>
    /// Creates a new instance of the context object.
    /// </summary>
    /// <param name="context">The HTTP request context</param>
    /// <param name="scheme">The scheme data</param>
    /// <param name="options">The handler options</param>
    /// <param name="principal">Initializes Principal property</param>
    /// <param name="isRefresh">Initializes <see cref="IsRefresh"/> indicating a refresh token was used to sign in.</param>
    /// <param name="properties">The authentication properties.</param>
    public SigningInContext(
        HttpContext context,
        AuthenticationScheme scheme,
        BearerTokenOptions options,
        ClaimsPrincipal principal,
        bool isRefresh,
        AuthenticationProperties? properties)
        : base(context, scheme, options, properties)
    {
        IsRefresh = isRefresh;
        Principal = principal;
    }

    /// <summary>
    /// <see langword="true"/> if the sign in operation set <see cref="AuthenticationProperties.RefreshToken"/>.
    /// </summary>
    public bool IsRefresh { get; }

    /// <summary>
    /// The opaque bearer token to be written to the JSON response body as the "access_token".
    /// If left unset, one will be generated automatically.
    /// This should later be sent as part of the Authorization request header.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The opaque refresh token written the to the JSON response body as the "refresh_token".
    /// If left unset, it will be generated automatically.
    /// This should later be sent as part of a request to refresh the <see cref="AccessToken"/>
    /// </summary>
    public string? RefreshToken { get; set; }
}
