// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Testing
{
    public static class LogAssert
    {
        public static void MaxLogLevel(IEnumerable<WriteContext> writeContexts, LogLevel maxLogLevel)
        {
            // REVIEW: Should we include all logs exceeding maxLogLevel? Or is the first sufficient?
            if (writeContexts.FirstOrDefault(w => w.LogLevel > maxLogLevel) is WriteContext writeContext)
            {
                var logMessage = writeContext.Formatter(writeContext.State, writeContext.Exception);
                throw new XunitException($"Unexpected log: {writeContext.LogLevel} {writeContext.LoggerName}: {logMessage}");
            }
        }
    }
}
