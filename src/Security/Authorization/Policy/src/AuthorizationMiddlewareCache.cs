// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Authorization.Policy;

internal sealed class AuthorizationMiddlewareCache : IDisposable
{
    // Caches AuthorizationPolicy instances and the presence of valid IAllowAnonymous metadata.
    private readonly DataSourceDependentCache<ConcurrentDictionary<Endpoint, AuthorizationMiddlewareCacheEntry>> _cache;

    public AuthorizationMiddlewareCache(EndpointDataSource dataSource)
    {
        // We cache AuthorizationPolicy instances per-Endpoint for performance, but we want to wipe out
        // that cache if the endpoints change so that we don't allow unbounded memory growth.
        _cache = new DataSourceDependentCache<ConcurrentDictionary<Endpoint, AuthorizationMiddlewareCacheEntry>>(dataSource, (_) =>
        {
            // We don't eagerly fill this cache because there's no real reason to.
            return new ConcurrentDictionary<Endpoint, AuthorizationMiddlewareCacheEntry>();
        });
        _cache.EnsureInitialized();
    }

    public bool TryGet(Endpoint endpoint, out AuthorizationMiddlewareCacheEntry entry)
    {
        return _cache.Value!.TryGetValue(endpoint, out entry);
    }

    public void Store(Endpoint endpoint, AuthorizationMiddlewareCacheEntry policy)
    {
        _cache.Value![endpoint] = policy;
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
