// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
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
    public TimeSpan AccessTokenExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The TicketDataFormat is used to protect and unprotect the identity and other properties which are stored in the
    /// cookie value. If not provided one will be created using <see cref="DataProtectionProvider"/>.
    /// </summary>
    public ISecureDataFormat<AuthenticationTicket>? TicketDataFormat { get; set; }

    /// <summary>
    /// If set, and <see cref="TicketDataFormat"/> is not set, this will be used by the CookieAuthenticationHandler for data protection.
    /// </summary>
    public IDataProtectionProvider? DataProtectionProvider { get; set; }
}
