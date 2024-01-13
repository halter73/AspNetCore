// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.Routing;

internal sealed class SupplyParameterFromQueryValueProvider : ICascadingValueSupplier, IDisposable
{
    private readonly QueryParameterValueSupplier _queryParameterValueSupplier = new();
    private readonly NavigationManager _navigationManager;
    private HashSet<ComponentState>? _subscribers;
    private bool _isSubscribedToLocationChanges;
    private string? _lastUri;

    public SupplyParameterFromQueryValueProvider(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public bool IsFixed => false;

    public bool CanSupplyValue(in CascadingParameterInfo parameterInfo)
        => parameterInfo.Attribute is SupplyParameterFromQueryAttribute;

    public object? GetCurrentValue(in CascadingParameterInfo parameterInfo)
    {
        if (_navigationManager.Uri != _lastUri)
        {
            UpdateQueryParameters();
            _lastUri = _navigationManager.Uri;
        }

        var attribute = (SupplyParameterFromQueryAttribute)parameterInfo.Attribute; // Must be a valid cast because we check in CanSupplyValue
        var queryParameterName = attribute.Name ?? parameterInfo.PropertyName;
        return _queryParameterValueSupplier.GetQueryParameterValue(parameterInfo.PropertyType, queryParameterName);
    }

    public void Subscribe(ComponentState subscriber, in CascadingParameterInfo parameterInfo)
    {
        SubscribeToLocationChanges();

        _subscribers ??= new();
        _subscribers.Add(subscriber);
    }

    public void Unsubscribe(ComponentState subscriber, in CascadingParameterInfo parameterInfo)
    {
        _subscribers!.Remove(subscriber);

        if (_subscribers.Count == 0)
        {
            UnsubscribeFromLocationChanges();
        }
    }

    private void UpdateQueryParameters()
    {
        var query = GetQueryString(_navigationManager.Uri);

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

    private void SubscribeToLocationChanges()
    {
        if (_isSubscribedToLocationChanges)
        {
            return;
        }

        _isSubscribedToLocationChanges = true;
        _navigationManager.LocationChanged += OnLocationChanged;
    }

    private void UnsubscribeFromLocationChanges()
    {
        if (!_isSubscribedToLocationChanges)
        {
            return;
        }

        _isSubscribedToLocationChanges = false;
        _navigationManager.LocationChanged -= OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_subscribers is not null && _navigationManager.Uri != _lastUri)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.NotifyCascadingValueChanged(ParameterViewLifetime.Unbound);
            }
        }
    }

    public void Dispose()
    {
        UnsubscribeFromLocationChanges();
    }
}
