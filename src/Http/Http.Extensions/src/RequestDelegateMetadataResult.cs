// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// The metadata inferred by <see cref="RequestDelegateFactory.InferMetadata(System.Reflection.MethodInfo, Microsoft.AspNetCore.Http.RequestDelegateFactoryOptions?)"/>.
/// <see cref="RequestDelegateFactoryOptions.EndpointBuilder"/> will be automatically populated with this metadata if provided.
/// </summary>
public sealed class RequestDelegateMetadataResult
{
    /// <summary>
    /// Gets endpoint metadata inferred from creating the <see cref="RequestDelegate" />. If a non-null
    /// RequestDelegateFactoryOptions.EndpointMetadata list was passed in, this will be the same instance.
    /// </summary>
    public required IReadOnlyList<object> EndpointMetadata { get; init; }
}
