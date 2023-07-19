// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Identity.DTO;

internal sealed class ConfigureTwoFactorRequest
{
    public required string TwoFactorCode { get; init; }
    public required bool Enable { get; set; }
}
