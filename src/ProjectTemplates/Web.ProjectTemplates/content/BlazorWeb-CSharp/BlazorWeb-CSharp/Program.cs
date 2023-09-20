#if (IndividualLocalAuth)
using Microsoft.AspNetCore.Components.Authorization;
#if (!UseServer && !UseWebAssembly)
using Microsoft.AspNetCore.Components.Server;
#endif
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using BlazorWeb_CSharp;
#endif
#if (UseWebAssembly)
using BlazorWeb_CSharp.Client.Pages;
#endif
using BlazorWeb_CSharp.Components;
#if (IndividualLocalAuth)
using BlazorWeb_CSharp.Data;
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#if (!UseServer && !UseWebAssembly)
builder.Services.AddRazorComponents();
#else
builder.Services.AddRazorComponents()
    #if (UseServer && UseWebAssembly)
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
    #elif (UseServer)
    .AddInteractiveServerComponents();
    #elif (UseWebAssembly)
    .AddInteractiveWebAssemblyComponents();
    #endif
#endif

#if (IndividualLocalAuth)
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<UserAccessor>();
#if (UseServer && UseWebAssembly)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();
#elif (UseServer)
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
#elif (UseWebAssembly)
builder.Services.AddScoped<AuthenticationStateProvider, PersistingServerAuthenticationStateProvider>();
#else
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddAuthorization();
#endif

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
#if (UseLocalDB)
    options.UseSqlServer(connectionString));
#else
    options.UseSqlite(connectionString));
#endif
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();

#endif
var app = builder.Build();

// Configure the HTTP request pipeline.
#if (UseWebAssembly)
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
#else
if (!app.Environment.IsDevelopment())
#endif
{
    app.UseExceptionHandler("/Error");
#if (HasHttpsProfile)
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
#endif
}

#if (HasHttpsProfile)
app.UseHttpsRedirection();

#endif
app.UseStaticFiles();

#if (UseServer && UseWebAssembly)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);
#elif (UseServer)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
#elif (UseWebAssembly)
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);
#else
app.MapRazorComponents<App>();
#endif

#if (IndividualLocalAuth)
// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

#endif
app.Run();
