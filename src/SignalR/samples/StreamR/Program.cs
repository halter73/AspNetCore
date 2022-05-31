// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using StreamR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMvc();
builder.Services.AddSignalR();
builder.Services.AddSingleton<StreamManager>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.MapRazorPages();
app.MapHub<StreamHub>("/stream");

app.Run();
