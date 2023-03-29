// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity.Endpoints.DTO;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Identity.Endpoints;

internal sealed class IdentityEndpointJsonOptionsSetup : IConfigureOptions<JsonOptions>
{
    public void Configure(JsonOptions options)
    {
        // Put our resolver in front of the reflection-based one. See ProblemDetailsOptionsSetup for a detailed explanation.
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, IdentityEndpointJsonSerializerContext.Default);
    }
}
