// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.Routing;

internal sealed class SupplyParameterFromQueryValueProvider(NavigationManager navigationManager) : ICascadingValueSupplier, IDisposable
{
    private QueryParameterValueSupplier? _queryParameterValueSupplier;
    private HashSet<ComponentState>? _subscribers;
    private HashSet<ComponentState>? _pendingSubscribers;
    private string? _lastUri;

    public bool IsFixed => false;

    public bool CanSupplyValue(in CascadingParameterInfo parameterInfo)
        => parameterInfo.Attribute is SupplyParameterFromQueryAttribute;

    public object? GetCurrentValue(in CascadingParameterInfo parameterInfo)
    {
        // Router.OnLocationChanged calls this before our own OnLocationChanged handler,
        // so we have to compare strings rather than rely on a bool set in OnLocationChanged.
        if (navigationManager.Uri != _lastUri)
        {
            UpdateQueryParameters();
            _lastUri = navigationManager.Uri;
        }

        Debug.Assert(_queryParameterValueSupplier is not null);
        var attribute = (SupplyParameterFromQueryAttribute)parameterInfo.Attribute; // Must be a valid cast because we check in CanSupplyValue
        var queryParameterName = attribute.Name ?? parameterInfo.PropertyName;
        return _queryParameterValueSupplier.GetQueryParameterValue(parameterInfo.PropertyType, queryParameterName);
    }

    public void Subscribe(ComponentState subscriber, in CascadingParameterInfo parameterInfo)
    {
        if (_pendingSubscribers?.Count > 0 || (_subscribers?.Count > 0 && navigationManager.Uri != _lastUri))
        {
            // This branch is only taken if there's a pending OnLocationChanged event for the current Uri.
            _pendingSubscribers ??= new();
            _pendingSubscribers.Add(subscriber);
            return;
        }

        _subscribers ??= new();
        _subscribers.Add(subscriber);

        if (_subscribers.Count == 1)
        {
            navigationManager.LocationChanged += OnLocationChanged;
        }
    }

    public void Unsubscribe(ComponentState subscriber, in CascadingParameterInfo parameterInfo)
    {
        Debug.Assert(_subscribers is not null);
        _pendingSubscribers?.Remove(subscriber);

        if (_subscribers?.Remove(subscriber) == true && _subscribers.Count == 0)
        {
            navigationManager.LocationChanged -= OnLocationChanged;
        }
    }

    private void UpdateQueryParameters()
    {
        var query = GetQueryString(navigationManager.Uri);

        _queryParameterValueSupplier ??= new();
        _queryParameterValueSupplier.ReadParametersFromQuery(query);

        static ReadOnlyMemory<char> GetQueryString(string url)
        {
            var queryStartPos = url.IndexOf('?');

            if (queryStartPos < 0)
            {
                return default;
            }

            var queryEndPos = url.IndexOf('#', queryStartPos);
            return url.AsMemory(queryStartPos..(queryEndPos < 0 ? url.Length : queryEndPos));
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        Debug.Assert(_subscribers is not null);
        foreach (var subscriber in _subscribers)
        {
            subscriber.NotifyCascadingValueChanged(ParameterViewLifetime.Unbound);
        }

        if (_pendingSubscribers is not null)
        {
            foreach (var subscriber in _pendingSubscribers)
            {
                _subscribers.Add(subscriber);
            }

            _pendingSubscribers.Clear();
        }
    }

    public void Dispose()
    {
        if (_subscribers?.Count > 0)
        {
            navigationManager.LocationChanged -= OnLocationChanged;
        }
    }
}
