// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add identity endpoints.
/// </summary>
public static class IdentityEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapIdentity<TUser>(this IEndpointRouteBuilder endpoints) where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var v1 = endpoints.MapGroup("/v1");

        var clock = endpoints.ServiceProvider.GetRequiredService<ISystemClock>();
        var bearerOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<IdentityBearerAuthenticationOptions>>().Value;
        var expireTimeSpan = bearerOptions.AccessTokenExpireTimeSpan;

        var dataProtection = endpoints.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection.CreateProtector(IdentityConstants.BearerScheme, "v1", "AccessToken");
        var ticketProtector = new TicketDataFormat(protector);

        // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
        // https://github.com/dotnet/aspnetcore/issues/47338
        v1.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] RegisterDTO registration, [FromServices] IServiceProvider services) =>
        {
            var userManager = services.GetRequiredService<UserManager<TUser>>();

            var user = new TUser();
            await userManager.SetUserNameAsync(user, registration.UserName);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (result.Succeeded)
            {
                // TODO: Send email confirmation

                return TypedResults.Ok();
            }

            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        });

        v1.MapPost("/login", async Task<Results<UnauthorizedHttpResult, Ok<AuthTokensDTO>, SignInHttpResult>>
            ([FromBody] LoginDTO login, [FromServices] IServiceProvider services) =>
        {
            var userManager = services.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByNameAsync(login.UserName);

            if (user is null || !await userManager.CheckPasswordAsync(user, login.Password))
            {
                return TypedResults.Unauthorized();
            }

            var claimsFactory = services.GetRequiredService<IUserClaimsPrincipalFactory<TUser>>();
            var claimsPrincipal = await claimsFactory.CreateAsync(user);

            if (login.CookieMode)
            {
                return TypedResults.SignIn(claimsPrincipal,
                    authenticationScheme: IdentityConstants.ApplicationScheme);
            }

            var properties = new AuthenticationProperties
            {
                ExpiresUtc = clock.UtcNow + expireTimeSpan
            };

            var ticket = new AuthenticationTicket(claimsPrincipal, properties, IdentityConstants.BearerScheme);

            return TypedResults.Ok(new AuthTokensDTO { AccessToken = ticketProtector.Protect(ticket) });
        });

        return new IdentityEndpointConventionBuilder(v1);
    }

    // Wrap RouteGroupBuilder with a non-public type to avoid a potential future behavioral breaking change.
    private sealed class IdentityEndpointConventionBuilder(RouteGroupBuilder inner) : IEndpointConventionBuilder
    {
        private readonly IEndpointConventionBuilder _inner = inner;

        public void Add(Action<EndpointBuilder> convention) => _inner.Add(convention);
        public void Finally(Action<EndpointBuilder> finallyConvention) => _inner.Finally(finallyConvention);
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromBodyAttribute : Attribute, IFromBodyMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromServicesAttribute : Attribute, IFromServiceMetadata
    {
    }
}

// TODO: Register DTOs with JsonSerializerOptions.TypeInfoResolverChain (was previously the soon-to-be-obsolete AddContext)
internal sealed class RegisterDTO
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    // TODO: public string? Email { get; set; }
}

internal sealed class LoginDTO
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public bool CookieMode { get; init; }
    // TODO: public string? TfaCode { get; set; }
}

internal sealed class AuthTokensDTO
{
    public required string AccessToken { get; init; }
    // TODO: public required string RefreshToken { get; init; }
}

[JsonSerializable(typeof(RegisterDTO))]
[JsonSerializable(typeof(LoginDTO))]
[JsonSerializable(typeof(AuthTokensDTO))]
internal sealed partial class IdentityEndpointJsonSerializerContext : JsonSerializerContext
{
}
