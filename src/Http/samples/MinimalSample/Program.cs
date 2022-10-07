// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

var app = WebApplication.Create(args);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

string Plaintext() => "Hello, World!";
app.MapGet("/plaintext", Plaintext);


object Json() => new { message = "Hello, World!" };
app.MapGet("/json", Json);

string SayHello(string name) => $"Hello, {name}!";
app.MapGet("/hello/{name}", SayHello);

var extensions = new Dictionary<string, object>() { { "traceId", "traceId123" } };

app.MapGet("/problem", () =>
    Results.Problem(statusCode: 500, extensions: extensions));

app.MapGet("/problem-object", () =>
    Results.Problem(new ProblemDetails() { Status = 500, Extensions = { { "traceId", "traceId123"} } }));

var errors = new Dictionary<string, string[]>();

app.MapGet("/validation-problem", () =>
    Results.ValidationProblem(errors, statusCode: 400, extensions: extensions));

app.MapGet("/validation-problem-object", () =>
    Results.Problem(new HttpValidationProblemDetails(errors) { Status = 400, Extensions = { { "traceId", "traceId123"}}}));

((IEndpointRouteBuilder)app).DataSources.Add(new DynamicEndpointDataSource());

app.Run();

public class DynamicEndpointDataSource : EndpointDataSource, IDisposable
{
    private CancellationTokenSource _cts = new();
    private Endpoint[] _endpoints = Array.Empty<Endpoint>();
    private PeriodicTimer _timer;
    private Task _timerTask;

    public DynamicEndpointDataSource()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _timerTask = TimerLoop();
    }

    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    public async Task TimerLoop()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            var oldLength = _endpoints.Length;

            var newEndpoints = new Endpoint[oldLength + 1];
            Array.Copy(_endpoints, 0, newEndpoints, 0, oldLength);

            newEndpoints[oldLength] = new RouteEndpoint(context => context.Response.WriteAsync($"Dynamic endpoint #{oldLength}"),
                RoutePatternFactory.Parse($"/dynamic/{oldLength}"), 0, null, null);

            _endpoints = newEndpoints;

            _cts.Cancel();
            _cts = new CancellationTokenSource();
            //var lastCts = _cts;
            //_cts = new CancellationTokenSource();
            //lastCts.Cancel();
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _timerTask.GetAwaiter().GetResult();
    }

    public override IChangeToken GetChangeToken()
    {
        return new CancellationChangeToken(_cts.Token);
    }
}
