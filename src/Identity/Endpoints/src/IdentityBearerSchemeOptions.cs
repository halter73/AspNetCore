// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Identity.Endpoints;

/// <summary>
/// Contains the options used to authenticate using bearer tokens issued by <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/>.
/// </summary>
public sealed class IdentityBearerAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Controls how much time the bearer token will remain valid from the point it is created.
    /// The expiration information is stored in the protected token. Because of that, an expired token will be rejected
    /// even if it is passed to the server after the client should have purged it.
    /// </summary>
    public TimeSpan AccessTokenExpireTimeSpan { get; set; } = TimeSpan.FromHours(1);
}
