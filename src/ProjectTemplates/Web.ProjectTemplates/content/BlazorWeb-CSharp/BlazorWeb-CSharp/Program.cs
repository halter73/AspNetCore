using BlazorWeb_CSharp.Components;
#if (IndividualLocalAuth)
using BlazorWeb_CSharp;
using BlazorWeb_CSharp.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#if (!UseServer && !UseWebAssembly)
builder.Services.AddRazorComponents();
#else
builder.Services.AddRazorComponents()
#if (UseServer && UseWebAssembly)
    .AddServerComponents()
    .AddWebAssemblyComponents();
#elif (UseServer)
    .AddServerComponents();
#elif (UseWebAssembly)
    .AddWebAssemblyComponents();
#endif
#endif

#if (IndividualLocalAuth)
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();

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

builder.Services.AddIdentityCore<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
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
    .AddServerRenderMode()
    .AddWebAssemblyRenderMode();
#elif (UseServer)
app.MapRazorComponents<App>()
    .AddServerRenderMode();
#elif (UseWebAssembly)
app.MapRazorComponents<App>()
    .AddWebAssemblyRenderMode();
#else
app.MapRazorComponents<App>();
#endif

app.Run();
