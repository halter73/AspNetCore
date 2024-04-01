// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Components.TestServer.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSingleton<AsyncOperationService>();
builder.Logging.SetMinimumLevel(LogLevel.Trace);

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger<Program>();

logger.LogInformation("Logged after the app is built in the Program file.");

await host.RunAsync();
