// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Identity.DefaultUI.WebSite;
using Identity.DefaultUI.WebSite.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Identity.FunctionalTests;

public class MapIdentityTests : LoggedTest
{
    [Fact]
    public async Task CanRegisterUser()
    {
        await using var app = await CreateAppAsync<ApplicationUser, ApplicationDbContext>();
        using var client = app.GetTestClient();

        var userName = $"{Guid.NewGuid()}@example.com";
        var password = $"[PLACEHOLDER]-1a";

        var response = await client.PostAsJsonAsync("/identity/v1/register", new { userName, password });

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task CanLogin()
    {
        await using var app = await CreateAppAsync<ApplicationUser, ApplicationDbContext>();
        using var client = app.GetTestClient();

        var userName = $"{Guid.NewGuid()}@example.com";
        var password = $"[PLACEHOLDER]-1a";

        var registerResponse = await client.PostAsJsonAsync("/identity/v1/register", new { userName, password });

        registerResponse.EnsureSuccessStatusCode();
        Assert.Equal(0, registerResponse.Content.Headers.ContentLength);

        var loginResponse = await client.PostAsJsonAsync("/identity/v1/login", new { userName, password });

        loginResponse.EnsureSuccessStatusCode();
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("accessToken").ToString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {userName}!", await client.GetStringAsync("/auth/hello"));
    }

    private async Task<WebApplication> CreateAppAsync<TUser, TContext>()
        where TUser : class, new()
        where TContext : DbContext
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(LoggerFactory);

        builder.Services.AddIdentityEndpoints<TUser>().AddEntityFrameworkStores<TContext>();
        builder.Services.AddAuthorization();

        var dbConnection = new SqliteConnection($"DataSource=:memory:");
        builder.Services.AddDbContext<TContext>(options => options.UseSqlite(dbConnection));
        // Dispose SqliteConnection with host by registering as a singleton factory.
        builder.Services.AddSingleton(() => dbConnection);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGroup("/identity").MapIdentity<TUser>();

        var authGroup = app.MapGroup("/auth").RequireAuthorization();
        authGroup.MapGet("/hello",
            (ClaimsPrincipal p) => $"Hello, {p.Identity.Name}!");

        await dbConnection.OpenAsync();
        await app.Services.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();

        return app;
    }
}
