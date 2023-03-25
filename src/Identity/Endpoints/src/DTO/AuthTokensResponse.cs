// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Identity.Endpoints.DTO;

internal sealed class AuthTokensResponse
{
    [JsonPropertyName("token_type")]
    public static string TokenType => "Bearer";

    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    // TODO: public required string RefreshToken { get; init; }
}
