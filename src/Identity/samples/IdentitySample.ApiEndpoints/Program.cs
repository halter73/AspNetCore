// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IdentityDbContext<IdentityUser>>(options =>
{
    options.UseSqlite(connection);
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddUserManager<UserManager<IdentityUser>>()
    .AddRoleManager<RoleManager<IdentityRole>>()
    .AddEntityFrameworkStores<IdentityDbContext<IdentityUser>>();

var app = builder.Build();
await CreateDummyData();

app.MapIdentityApi<IdentityUser>();

app.Run();
return;

async Task CreateDummyData()
{
    const string email = "test@example.com";
    const string password = "Password123!";
    const string role1 = "admin";
    const string role2 = "user";

    // Create database 
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext<IdentityUser>>();
    dbContext.Database.EnsureDeleted();
    dbContext.Database.EnsureCreated();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await userManager.CreateAsync(new IdentityUser(email));
    var user = await userManager.FindByNameAsync(email);
    await userManager.AddPasswordAsync(user, password);
    await userManager.SetEmailAsync(user, email);
    await roleManager.CreateAsync(new IdentityRole(role1));
    await roleManager.CreateAsync(new IdentityRole(role2));
    await userManager.AddToRoleAsync(user, role1);
    await userManager.AddToRoleAsync(user, role2);
}
