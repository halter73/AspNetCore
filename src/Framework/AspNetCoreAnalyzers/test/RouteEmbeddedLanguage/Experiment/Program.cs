// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#region Namespaces
using Microsoft.AspNetCore.Analyzers.RouteEmbeddedLanguage.Experiment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#endregion

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();
var db = new DbContext();

app.MapGet("/users/{userId}/books/{bookId?}", IResult (int userId, int? bookId) =>
{
    return bookId != null
        ? Results.Ok(db.Books.Find(bookId.Value))
        : Results.Ok(db.Books);
});

app.MapGet("/posts/{**rest}", (string rest) => $"Routing to {rest}");
app.MapGet("/todos/{id}", (int id) => db.Todos.Find(id));
app.MapGet("/todos/{text}", (string text) => db.Todos.Where(t => t.Text.Contains(text)));
app.MapGet("/posts/{slug:regex(^[a-z0-9_-]+$)}", (string slug) => $"Post {slug}");

app.MapControllerRoute("Default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
