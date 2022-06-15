// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;

namespace Microsoft.AspNetCore.OutputCaching;

/// <summary>
/// A set of endpoint extension methods.
/// </summary>
public static class PolicyExtensions
{
    /// <summary>
    /// Marks an endpoint to be cached with the default policy.
    /// </summary>
    public static TBuilder CacheOutput<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Enable caching if this method is invoked on an endpoint, extra policies can disable it

        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(DefaultOutputCachePolicy.Instance);
        });
        return builder;
    }

    /// <summary>
    /// Marks an endpoint to be cached with the specified policy.
    /// </summary>
    public static TBuilder CacheOutput<TBuilder>(this TBuilder builder, IOutputCachePolicy policy) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Enable caching if this method is invoked on an endpoint, extra policies can disable it

        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(policy);
        });
        return builder;
    }

    /// <summary>
    /// Marks an endpoint to be cached using the specified policy builder.
    /// </summary>
    public static TBuilder CacheOutput<TBuilder>(this TBuilder builder, Action<OutputCachePolicyBuilder> policy) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var outputCachePolicyBuilder = new OutputCachePolicyBuilder();

        policy?.Invoke(outputCachePolicyBuilder);

        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(outputCachePolicyBuilder.Build());
        });

        return builder;
    }
}
