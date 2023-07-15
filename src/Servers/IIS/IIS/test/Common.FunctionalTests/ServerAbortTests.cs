// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.IIS.FunctionalTests
{
    [Collection(PublishedSitesCollection.Name)]
    public class ServerAbortOutOfProcessTests : IISFunctionalTestBase
    {
        public ServerAbortOutOfProcessTests(PublishedSitesFixture fixture) : base(fixture)
        {
        }

        [ConditionalFact]
        public async Task ClosesConnectionOnServerAbortOutOfProcess()
        {
            try
            {
                var deploymentParameters = Fixture.GetBaseDeploymentParameters(HostingModel.OutOfProcess);

                var deploymentResult = await DeployAsync(deploymentParameters);

                var response = await deploymentResult.HttpClient.GetAsync("/Abort").DefaultTimeout();

                Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

#if NEWSHIM_FUNCTIONALS
                // In-proc SocketConnection isn't used and there's no abort
                // 0x80072f78 ERROR_HTTP_INVALID_SERVER_RESPONSE The server returned an invalid or unrecognized response
                Assert.Contains("0x80072f78", await response.Content.ReadAsStringAsync());
#else
                // 0x80072efe ERROR_INTERNET_CONNECTION_ABORTED The connection with the server was terminated abnormally
                Assert.Contains("0x80072efe", await response.Content.ReadAsStringAsync());
#endif
            }
            catch (HttpRequestException)
            {
                // Connection reset is expected
            }
        }

        [ConditionalFact]
        public async Task ClosesConnectionOnServerAbortInProcess()
        {
            try
            {
                var deploymentParameters = Fixture.GetBaseDeploymentParameters(HostingModel.InProcess);

                var deploymentResult = await DeployAsync(deploymentParameters);
                var response = await deploymentResult.HttpClient.GetAsync("/Abort").DefaultTimeout();

                Assert.True(false, "Should not reach here");
            }
            catch (HttpRequestException)
            {
                // Connection reset is expected both for outofproc and inproc
            }
        }
    }
}
