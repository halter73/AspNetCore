﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// A resource of the component application, such as a script, stylesheet or image.
/// </summary>
/// <param name="url">The URL of the resource.</param>
/// <param name="properties">The properties associated to this resource.</param>
public class ResourceAsset(string url, IReadOnlyList<ResourceAssetProperty>? properties)
{
    /// <summary>
    /// Gets the URL that identifies this resource.
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    /// Gets a list of properties associated to this resource.
    /// </summary>
    public IReadOnlyList<ResourceAssetProperty>? Properties { get; } = properties;
}
