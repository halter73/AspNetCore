// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.OutputCaching;

/// <summary>
/// Specifies the parameters necessary for setting appropriate headers in output caching.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class OutputCacheAttribute : Attribute, IOutputCachePolicy
{
    // A nullable-int cannot be used as an Attribute parameter.
    // Hence this nullable-int is present to back the Duration property.
    // The same goes for nullable-ResponseCacheLocation and nullable-bool.
    private int? _duration;
    private bool? _noCache;

    private IOutputCachePolicy? _policy;

    /// <summary>
    /// Gets or sets the duration in seconds for which the response is cached.
    /// </summary>
    public int Duration
    {
        get => _duration ?? 0;
        set => _duration = value;
    }

    /// <summary>
    /// Gets or sets the value which determines whether the reponse should be cached or not.
    /// When set to <see langword="true"/>, the response won't be cached.
    /// </summary>
    public bool NoCache
    {
        get => _noCache ?? false;
        set => _noCache = value;
    }

    /// <summary>
    /// Gets or sets the query keys to vary by.
    /// </summary>
    /// <remarks>
    /// <see cref="VaryByQueryKeys"/> requires the output cache middleware.
    /// </remarks>
    public string[]? VaryByQueryKeys { get; set; }

    /// <summary>
    /// Gets or sets the headers to vary by.
    /// </summary>
    /// <remarks>
    /// <see cref="VaryByHeaders"/> requires the output cache middleware.
    /// </remarks>
    public string[]? VaryByHeaders { get; set; }

    /// <summary>
    /// Gets or sets the value of the cache policy name.
    /// </summary>
    public string? PolicyName { get; set; }

    Task IOutputCachePolicy.OnRequestAsync(OutputCacheContext context)
    {
        return GetPolicy().OnRequestAsync(context);
    }

    Task IOutputCachePolicy.OnServeFromCacheAsync(OutputCacheContext context)
    {
        return GetPolicy().OnServeFromCacheAsync(context);
    }

    Task IOutputCachePolicy.OnServeResponseAsync(OutputCacheContext context)
    {
        return GetPolicy().OnServeResponseAsync(context);
    }

    private IOutputCachePolicy GetPolicy()
    {
        if (_policy != null)
        {
            return _policy;
        }

        var builder = new OutputCachePolicyBuilder();

        if (_noCache != null && _noCache.Value)
        {
            builder.NoCache();
        }

        if (PolicyName != null)
        {
            builder.Policy(PolicyName);
        }

        if (VaryByQueryKeys != null)
        {
            builder.VaryByQuery(VaryByQueryKeys);
        }

        if (_duration != null)
        {
            builder.Expire(TimeSpan.FromSeconds(_duration.Value));
        }

        return _policy = builder.Build();
    }
}
