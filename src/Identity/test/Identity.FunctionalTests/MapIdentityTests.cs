// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Json;
using System.Security.Claims;
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
    public async Task CanRegisterAUser()
    {
        await using var app = await CreateAppAsync<ApplicationUser, ApplicationDbContext>();
        using var client = app.GetTestClient();

        var userName = $"{Guid.NewGuid()}@example.com";
        var password = $"[PLACEHOLDER]-1a";

        var response = await client.PostAsJsonAsync("/identity/v1/register", new { userName, password });

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    private async Task<WebApplication> CreateAppAsync<TUser, TContext>()
        where TUser : class, new()
        where TContext : DbContext
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(LoggerFactory);

        builder.Services.AddIdentityCore<TUser>()
            .AddEntityFrameworkStores<TContext>();
        //builder.Services.AddScoped<IUserClaimsPrincipalFactory<TUser>, UserClaimsPrincipalFactory<TUser>>();

        // Dispose SqliteConnection with host.
        var dbConnection = new SqliteConnection($"DataSource=:memory:");
        await dbConnection.OpenAsync();
        builder.Services.AddDbContext<TContext>(options => options.UseSqlite(dbConnection));
        builder.Services.AddSingleton(() => dbConnection);

        var app = builder.Build();

        app.MapGroup("/identity").MapIdentity<TUser>();

        var authGroup = app.MapGroup("/auth").RequireAuthorization();
        authGroup.MapGet("/hello", (ClaimsPrincipal principal) => $"Hello, {principal.Identity.Name}!");

        await app.Services.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();

        return app;
    }
}
