// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Components.TestServer.RazorComponents;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using TestServer;
using Microsoft.AspNetCore.E2ETesting;
using Xunit.Abstractions;
using OpenQA.Selenium;

namespace Microsoft.AspNetCore.Components.E2ETests.ServerRenderingTests.AuthTests;

public class ServerRenderedAuthenticationStateTest
     : ServerTestBase<BasicTestAppServerSiteFixture<RazorComponentEndpointsStartup<App>>>
{
    public ServerRenderedAuthenticationStateTest(
        BrowserFixture browserFixture,
        BasicTestAppServerSiteFixture<RazorComponentEndpointsStartup<App>> serverFixture,
        ITestOutputHelper output)
        : base(browserFixture, serverFixture, output)
    {
    }

    [Theory]
    [InlineData("Static")]
    [InlineData("Server")]
    [InlineData("WebAssembly")]
    public void CanUseServerAuthenticationState(string platform)
    {
        var isInteractive = platform != "Static";

        void VerifyPlatform()
        {
            Browser.Equal(isInteractive.ToString(), () => Browser.FindElement(By.Id("is-interactive")).Text);
            Browser.Equal(platform, () => Browser.FindElement(By.Id("platform")).Text);
        }

        void VerifyLoggedOut()
        {
            VerifyPlatform();
            Browser.Equal("False", () => Browser.FindElement(By.Id("identity-authenticated")).Text);
            Browser.Equal("", () => Browser.FindElement(By.Id("identity-name")).Text);
            Browser.Equal("(none)", () => Browser.FindElement(By.Id("test-claim")).Text);
        }

        void VerifyLoggedIn()
        {
            VerifyPlatform();
            Browser.Equal("True", () => Browser.FindElement(By.Id("identity-authenticated")).Text);
            Browser.Equal("YourUsername", () => Browser.FindElement(By.Id("identity-name")).Text);
            Browser.Equal("Test claim value", () => Browser.FindElement(By.Id("test-claim")).Text);
        }

        Navigate($"{ServerPathBase}/auth/{platform}{(isInteractive ? "-interactive" : "")}-authentication-state");

        VerifyLoggedOut();
        
        Browser.Click(By.LinkText("Log in"));

        VerifyLoggedIn();

        Browser.Click(By.LinkText("Log out"));

        VerifyLoggedOut();
    }
}
