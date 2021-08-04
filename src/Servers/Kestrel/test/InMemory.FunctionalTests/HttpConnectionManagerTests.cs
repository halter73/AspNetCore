// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests
{
    public class HttpConnectionManagerTests : LoggedTest
    {
        // This test causes MemoryPoolBlocks to be finalized which in turn causes an assert failure in debug builds.
#if !DEBUG
        [ConditionalFact]
        public async Task CriticalErrorLoggedIfApplicationDoesntComplete()
        {
            var logWh = new SemaphoreSlim(0);
            var appStartedWh = new SemaphoreSlim(0);

            var mockTrace = new Mock<KestrelTrace>(LoggerFactory) { CallBase = true };
            mockTrace
                .Setup(trace => trace.ApplicationNeverCompleted(It.IsAny<string>()))
                .Callback(() =>
                {
                    logWh.Release();
                });

            var testContext = new TestServiceContext(new LoggerFactory(), mockTrace.Object);
            testContext.InitializeHeartbeat();

            await using (var server = new TestServer(async context =>
                {
                    appStartedWh.Release();
                    await new NeverCompleteAwaitable();
                },
                testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.SendEmptyGet();

                    Assert.True(await appStartedWh.WaitAsync(TestConstants.DefaultTimeout));

                    // Close connection without waiting for a response
                }

                var logWaitAttempts = 0;

                for (; !await logWh.WaitAsync(TimeSpan.FromSeconds(1)) && logWaitAttempts < 30; logWaitAttempts++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Assert.True(logWaitAttempts < 10);
            }
        }
#endif

        //////////////////////////////////////////////////////////////////////////////////////////
        // WARNING: Use a custom awaitable instead of a TaskCompletionSource because            //
        //          Task.s_currentActiveTasks roots HttpConnection under a debugger with a TCS. //
        //////////////////////////////////////////////////////////////////////////////////////////
        internal class NeverCompleteAwaitable : ICriticalNotifyCompletion
        {
            public NeverCompleteAwaitable GetAwaiter() => this;
            public bool IsCompleted => false;

            public void GetResult()
            {
            }

            public void OnCompleted(Action continuation)
            {
            }

            public void UnsafeOnCompleted(Action continuation)
            {
            }
        }
    }
}
