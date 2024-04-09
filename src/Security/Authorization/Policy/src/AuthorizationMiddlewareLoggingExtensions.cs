// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Logging;

internal static partial class AuthorizationMiddlewareLoggingExtensions
{
    [LoggerMessage(0, LogLevel.Debug, "Policy authentication schemes {policyName} did not succeed", EventName = "PolicyAuthenticationSchemesDidNotSucceed")]
    private static partial void PolicyAuthenticationSchemesDidNotSucceed(this ILogger<AuthorizationMiddleware> logger, string policyName);

    [LoggerMessage(1, LogLevel.Warning, "Endpoint {endpoint} has IAllowAnonymous metadata which is being ignored because IAuthorizeData metadata is more local", EventName = "AllowAnonymousIgnored")]
    public static partial void AllowAnonymousIgnored(this ILogger<AuthorizationMiddleware> logger, Endpoint endpoint);

    public static void PolicyAuthenticationSchemesDidNotSucceed(this ILogger<AuthorizationMiddleware> logger, IReadOnlyList<string> schemes)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.PolicyAuthenticationSchemesDidNotSucceed(string.Join(", ", schemes));
    }
}
