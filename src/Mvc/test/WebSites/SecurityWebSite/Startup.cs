// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SecurityWebSite
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddControllersWithViews();
            services.AddAntiforgery();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.LoginPath = "/Home/Login";
                options.LogoutPath = "/Home/Logout";
            })
            .AddCookie("Cookie2");

            services.AddScoped<IPolicyEvaluator, CountingPolicyEvaluator>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.Run(async context => 
            {
                if (context.Request.Path.StartsWithSegments("api/products", StringComparison.Ordinal))
                {
                    var policyProvider = context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();

                    // We don't need to check which policy was selected by attributes/IAuthorizeData b/c there is none.
                    var fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

                    if (fallbackPolicy is null)
                    {
                        context.Response.StatusCode = 401;
                        return;
                    }

                    var policyEvaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();
                    var authenticateResult = await policyEvaluator.AuthenticateAsync(fallbackPolicy, context);
                    var authorizeResult = await policyEvaluator.AuthorizeAsync(fallbackPolicy, authenticateResult, context, resource: null);

                    if (!authorizeResult.Succeeded)
                    {
                        context.Response.StatusCode = 401;
                        return;
                    }


                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Hello World!" });
                }
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
