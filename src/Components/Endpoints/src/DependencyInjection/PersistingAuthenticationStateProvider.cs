// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.Endpoints.DependencyInjection;

internal sealed class PersistingAuthenticationStateProvider
    : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider, IDisposable
{
    private const string PersistenceKey = $"__internal__{nameof(AuthenticationState)}";

    private readonly PersistentComponentState _state;
    private readonly Func<AuthenticationState, Task<IEnumerable<KeyValuePair<string, string>>>> _serializeFunc;
    private readonly PersistingComponentStateSubscription _subscription;

    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingAuthenticationStateProvider(PersistentComponentState persistentComponentState, IOptions<RazorComponentsServiceOptions> razorOptions)
    {
        _state = persistentComponentState;
        _serializeFunc = razorOptions.Value.SerializeAuthenticationState
            ?? throw new ArgumentException($"{nameof(RazorComponentsServiceOptions)}.{nameof(RazorComponentsServiceOptions.SerializeAuthenticationState)} must not be null.");

        if (razorOptions.Value.SerializeAuthenticationStateToClient)
        {
            _subscription = persistentComponentState.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
        }
    }

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authenticationStateTask
        ?? throw new InvalidOperationException($"Do not call {nameof(GetAuthenticationStateAsync)} outside of the DI scope for a Razor component. Typically, this means you can call it only within a Razor component or inside another DI service that is resolved for a Razor component.");

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new InvalidOperationException($"{nameof(SetAuthenticationState)} must be called before the {nameof(PersistentComponentState)}.{nameof(PersistentComponentState.RegisterOnPersisting)} callback.");
        }

        _state.PersistAsJson(PersistenceKey, await _serializeFunc(await _authenticationStateTask));
    }

    /// <inheritdoc />
    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        _authenticationStateTask = authenticationStateTask ?? throw new ArgumentNullException(nameof(authenticationStateTask));
        NotifyAuthenticationStateChanged(_authenticationStateTask);
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
