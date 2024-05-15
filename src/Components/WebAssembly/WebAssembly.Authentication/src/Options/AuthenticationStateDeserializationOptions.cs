// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Microsoft.AspNetCore.Components.WebAssembly.Authentication;

/// <summary>
/// 
/// </summary>
public sealed class AuthenticationStateDeserializationOptions
{
    /// <summary>
    /// 
    /// </summary>
    public Func<IEnumerable<KeyValuePair<string, string>>, Task<AuthenticationState>> DeserializeAuthenticationState { get; set; } = DeserializeAuthenticationStateDefault;

    private static Task<AuthenticationState> DeserializeAuthenticationStateDefault(IEnumerable<KeyValuePair<string, string>> claims)
    {
        return Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Key, c.Value)),
                authenticationType: "DeserializedAuthenticationState"))));
    }
}
