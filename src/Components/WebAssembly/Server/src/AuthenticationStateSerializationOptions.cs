// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Authorization;

namespace Microsoft.AspNetCore.Components.WebAssembly.Server;

/// <summary>
/// 
/// </summary>
public class AuthenticationStateSerializationOptions
{
    /// <summary>
    /// 
    /// </summary>
    public Func<AuthenticationState, Task<IEnumerable<KeyValuePair<string, string>>>> SerializeCallback { get; set; } = SerializeClaimsAsync;

    private static Task<IEnumerable<KeyValuePair<string, string>>> SerializeClaimsAsync(AuthenticationState authenticationState)
        => Task.FromResult(SerializeClaims(authenticationState));

    private static IEnumerable<KeyValuePair<string, string>> SerializeClaims(AuthenticationState authenticationState)
    {
        foreach (var claim in authenticationState.User.Claims)
        {
            yield return new KeyValuePair<string, string>(claim.Type, claim.Value);
        }
    }
}
