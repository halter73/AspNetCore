// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = $"{nameof(PersistentAuthenticationStateProvider)} uses the {nameof(PersistentComponentState)} APIs to deserialize the token, which are already annotated.")]
    public PersistentAuthenticationStateProvider(PersistentComponentState state, IOptions<AuthenticationStateDeserializationOptions> options)
    {
        if (!state.TryTakeFromJson<IEnumerable<KeyValuePair<string, string>>>(PersistenceKey, out var claims) || claims is null || !claims.Any())
        {
            return;
        }

        _authenticationStateTask = options.Value.DeserializeAuthenticationState(claims);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
