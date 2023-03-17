// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
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
    /// <typeparam name="TUser">The type encapsulating the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
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
    /// <typeparam name="TUser">The type encapsulating the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <typeparam name="TUserKey">The type encapsulating the user. This should match the generic parameter <see cref="IdentityUser{TKey}"/>.</typeparam>
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
        group.MapPost("/register", async Task<Results<Ok, ValidationProblem>> ([FromBody] RegisterEndpointInfo info, [FromService] IServiceProvider services) =>
        {
            var userManager = services.GetRequiredService<UserManager<TUser>>();

            var user = new TUser();
            await userManager.SetUserNameAsync(user, info.Username);
            var result = await userManager.CreateAsync(user, info.Password);

            if (result.Succeeded)
            {
                return TypedResults.Ok();
            }

            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
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

    /// <summary>
    /// DTO for the register endpoint, username, password
    /// </summary>
    internal class RegisterEndpointInfo
    {
        /// <summary>
        /// The user name.
        /// </summary>
        [Required]
        public string Username { get; set; } = default!;

        /// <summary>
        /// The password
        /// </summary>
        [Required]
        public string Password { get; set; } = default!;

        /// <summary>
        /// The email for the user.
        /// </summary>
        public string Email { get; set; } = default!;
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private class FromBodyAttribute : Attribute, IFromBodyMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private class FromServiceAttribute : Attribute, IFromServiceMetadata
    {
    }
}
