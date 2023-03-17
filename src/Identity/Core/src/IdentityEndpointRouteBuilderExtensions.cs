// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add identity endpoints.
/// </summary>
public static class IdentityEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The <see cref="IdentityUser"/> type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapIdentity<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : IdentityUser<string>, new()
    {
        return endpoints.MapIdentity<TUser, string>();
    }

    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The <see cref="IdentityUser{TKey}"/> type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <typeparam name="TUserKey">The type of the user's primary key. This should match the generic parameter <see cref="IdentityUser{TKey}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapIdentity<TUser, TUserKey>(this IEndpointRouteBuilder endpoints)
        where TUser : IdentityUser<TUserKey>, new()
        where TUserKey : IEquatable<TUserKey>
    {
        // Call MapGroup yourself to get a prefix.
        var group = endpoints.MapGroup("");

        // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
        group.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] RegisterDTO registration, [FromServices] IServiceProvider services) =>
        {
            var userManager = services.GetRequiredService<UserManager<TUser>>();

            var user = new TUser();
            await userManager.SetUserNameAsync(user, registration.Username);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (result.Succeeded)
            {
                // TODO: Send email confirmation

                return TypedResults.Ok();
            }

            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        });

        group.MapPost("/login", async Task<Results<UnauthorizedHttpResult, Ok<AuthTokensDTO>, SignInHttpResult>>
            ([FromBody] LoginDTO login, [FromServices] IServiceProvider services) =>
        {
            var userManager = services.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByNameAsync(login.Username);

            if (user is null || !await userManager.CheckPasswordAsync(user, login.Password))
            {
                return TypedResults.Unauthorized();
            }

            var claimsFactory = services.GetRequiredService<IUserClaimsPrincipalFactory<TUser>>();

            return login.CookieMode
                ? TypedResults.SignIn(await claimsFactory.CreateAsync(user),
                    authenticationScheme: IdentityConstants.ApplicationScheme)
                : TypedResults.Ok(new AuthTokensDTO { AccessToken = "" });
        });

        return new IdentityEndpointConventionBuilder(group);
    }

    // If we return a public type like RouteGroupBuilder, it'd be breaking to change it even if it's declared to be a less specific type.
    // https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules#properties-fields-parameters-and-return-values
    private class IdentityEndpointConventionBuilder : IEndpointConventionBuilder
    {
        private readonly IEndpointConventionBuilder _inner;

        public IdentityEndpointConventionBuilder(IEndpointConventionBuilder inner)
        {
            _inner = inner;
        }

        public void Add(Action<EndpointBuilder> convention) => _inner.Add(convention);
        public void Finally(Action<EndpointBuilder> finallyConvention) => _inner.Finally(finallyConvention);
    }

    // NOTE: private classes cannot be used as parameter types with RDG Delegates.
    // TODO: Register DTOs with JsonSerializerOptions.TypeInfoResolverChain (was previously the soon-to-be-obsolete AddContext)
    internal class RegisterDTO
    {
        public required string Username { get; init; }
        public required string Password { get; init; }
        // TODO: public string? Email { get; set; }
    }

    internal class LoginDTO
    {
        public required string Username { get; init; }
        public required string Password { get; init; }
        public bool CookieMode { get; init; }
        // TODO: public string? TfaCode { get; set; }
    }

    internal class AuthTokensDTO
    {
        public required string AccessToken { get; init; }
        // TODO: public required string RefreshToken { get; init; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private class FromBodyAttribute : Attribute, IFromBodyMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private class FromServicesAttribute : Attribute, IFromServiceMetadata
    {
    }
}
