using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace HttpApiSampleApp
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapAction((Func<Todo, JsonResult>)EchoTodo);

                endpoints.MapPost("/EchoTodoProto", async httpContext =>
                {
                    var contentType = httpContext.Request.Headers[HeaderNames.ContentType].ToString();

                    if (!contentType.StartsWith("application/json", StringComparison.Ordinal))
                    {
                        httpContext.Response.StatusCode = 415;
                        return;
                    }

                    var todo = await httpContext.Request.ReadFromJsonAsync<Todo>();
                    await httpContext.Response.WriteAsJsonAsync(todo);
                });

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }

        [HttpPost("/EchoTodo")]
        private JsonResult EchoTodo([FromBody] Todo todo) => new JsonResult(todo);
    }
}
