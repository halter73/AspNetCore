// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlite(connection));

builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, bearerTokenOptions =>
{
    bearerTokenOptions.BearerTokenExpiration = TimeSpan.FromSeconds(1);
});

builder.Services.AddIdentityCore<IdentityUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddApiEndpoints();
//builder.Services.AddIdentityApiEndpoints<IdentityUser>()
//    .AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

app.MapGet("/", () => "Hello, World!");
app.MapGet("/requires-auth", (ClaimsPrincipal user) => $"Hello, {user.Identity?.Name}!").RequireAuthorization();

app.MapGroup("/identity").MapIdentityApi<IdentityUser>();
app.MapGet("/identity/logout", async context =>
{
    await context.SignOutAsync(IdentityConstants.ApplicationScheme);
});

app.Run();
connection.Close();

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }
}
