// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Components.TestServer.RazorComponents;

namespace TestServer;

public class RemoteAuthenticationStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.Map("/subdir", app =>
        {
            WebAssemblyTestHelper.ServeCoopHeadersIfWebAssemblyThreadingEnabled(app);
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAntiforgery();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorComponents<RemoteAuthenticationApp>()
                    .AddAdditionalAssemblies(Assembly.Load("Components.WasmRemoteAuthentication"))
                    .AddInteractiveWebAssemblyRenderMode(options => options.PathPrefix = "/WasmRemoteAuthentication");
            });
        });
    }
}
