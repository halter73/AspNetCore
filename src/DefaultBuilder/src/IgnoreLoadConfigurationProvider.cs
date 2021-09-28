// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore;

internal sealed class IgnoreLoadConfigurationProvider : IConfigurationProvider, IDisposable
{
    private readonly IConfigurationProvider _configurationProvider;

    public IgnoreLoadConfigurationProvider(IConfigurationProvider configurationProvider)
    {
        _configurationProvider = configurationProvider;
    }

    public IConfigurationProvider OriginalProvider => _configurationProvider;

    public void Load()
    {
        // ConfigurationManager has already loaded its IConfigurationProviders, so we do not need to load it again
        // during WebApplicationBuilder.Build(). See https://github.com/dotnet/aspnetcore/issues/37030
        // This is replaced by OriginalProvider at the end of ApplicationBuilder.Build().
    }

    public IChangeToken GetReloadToken() => _configurationProvider.GetReloadToken();

    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath) =>
        _configurationProvider.GetChildKeys(earlierKeys, parentPath);

    public void Set(string key, string value) => _configurationProvider.Set(key, value);

    public bool TryGet(string key, out string value) => _configurationProvider.TryGet(key, out value);

    public override string ToString() => _configurationProvider.ToString()!;

    public override bool Equals(object? obj) => _configurationProvider.Equals(obj);

    public override int GetHashCode() => _configurationProvider.GetHashCode();

    public void Dispose() => (_configurationProvider as IDisposable)?.Dispose();
}
