// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Authentication;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Http2SampleApp;

public class Program
{
    public static void Main(string[] args)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseKestrel(serverOptions => {
                        serverOptions.ListenAnyIP(443, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                            //listenOptions.UseHttps("./Server.pfx", "Pass1234");
                            listenOptions.UseHttps();
                        });
                    })
                    .UseStartup<Startup>();
            })
            .ConfigureLogging((_, factory) =>
            {
                factory.SetMinimumLevel(LogLevel.Warning);
                factory.AddConsole();
            });

        hostBuilder.Build().Run();
    }
}
