// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Identity.Endpoints;

/// <summary>
/// A context for <see cref="IdentityBearerEvents.OnExtractToken"/>.
/// </summary>
public class ExtractTokenContext : BaseContext<IdentityBearerOptions>
{
    /// <summary>
    /// Initializes a new instance of <see cref="ExtractTokenContext"/>.
    /// </summary>
    /// <inheritdoc />
    public ExtractTokenContext(
        HttpContext context,
        AuthenticationScheme scheme,
        IdentityBearerOptions options)
        : base(context, scheme, options) { }

    /// <summary>
    /// Bearer Token. This will give the application an opportunity to retrieve a token from an alternative location.
    /// </summary>
    public string? Token { get; set; }
}
