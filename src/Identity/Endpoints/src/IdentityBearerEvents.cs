// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Identity.Endpoints;

/// <summary>
/// Specifies events which the <see cref="IdentityEndpointRouteBuilderExtensions.MapIdentity{TUser}(IEndpointRouteBuilder)"/> invokes to enable developer control
/// over the identity bearer token authentication process.
/// </summary>
public class IdentityBearerEvents
{
    /// <summary>
    /// Invoked when the bearer token needs to be extracted from the HTTP request like during authentication.
    /// the bearer token from an alternate location by setting <see cref="ExtractTokenContext.Token"/>.
    /// </summary>
    public Func<ExtractTokenContext, Task> OnExtractToken { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked when a protocol message is first received before authenticating the bearer token. This can provide
    /// the bearer token from an alternate location by setting <see cref="ExtractTokenContext.Token"/>.
    /// </summary>
    public virtual Task ExtractToken(ExtractTokenContext context) => OnExtractToken(context);
}
