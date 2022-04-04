// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// 
/// </summary>
public interface IGroupEndpointDataSource
{
    /// <summary>
    /// 
    /// </summary>
    IEnumerable<IEndpointConventionBuilder> ConventionBuilders { get; }
}
