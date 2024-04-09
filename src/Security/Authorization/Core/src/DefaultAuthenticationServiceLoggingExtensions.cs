// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Extensions.Logging;

internal static partial class DefaultAuthenticationServiceLoggingExtensions
{
    [LoggerMessage(1, LogLevel.Debug, "Authorization was successful.", EventName = "UserAuthorizationSucceeded")]
    public static partial void UserAuthorizationSucceeded(this ILogger<DefaultAuthorizationService> logger);

    [LoggerMessage(2, LogLevel.Information, "Authorization failed. {Reason}", EventName = "UserAuthorizationFailed")]
    private static partial void UserAuthorizationFailed(this ILogger<DefaultAuthorizationService> logger, string reason);

    public static void UserAuthorizationFailed(this ILogger<DefaultAuthorizationService> logger, AuthorizationFailure failure)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var reason = failure.FailCalled
            ? "Fail() was explicitly called."
            : "These requirements were not met:" + Environment.NewLine + string.Join(Environment.NewLine, failure.FailedRequirements);

        logger.UserAuthorizationFailed(reason);
    }
}
