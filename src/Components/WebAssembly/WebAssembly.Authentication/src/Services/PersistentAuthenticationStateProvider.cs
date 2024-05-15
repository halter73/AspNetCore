// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.WebAssembly.Authentication;

internal sealed class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    // Do not change. This must match all versions of the server-side PersistingAuthenticationStateProvider.PersistenceKey.
    private const string PersistenceKey = $"__internal__{nameof(AuthenticationState)}";

    private static readonly Task<AuthenticationState> _defaultUnauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> _authenticationStateTask = _defaultUnauthenticatedTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState state, IOptions<AuthenticationStateDeserializationOptions> options)
    {
        if (!state.TryTakeFromJson<IEnumerable<KeyValuePair<string, string>>>(PersistenceKey, out var claims) || claims is null)
        {
            return;
        }

        _authenticationStateTask = options.Value.DeserializeAuthenticationState(claims);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
