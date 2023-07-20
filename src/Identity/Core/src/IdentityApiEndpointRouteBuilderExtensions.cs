// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.BearerToken.DTO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.DTO;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add identity endpoints.
/// </summary>
public static class IdentityApiEndpointRouteBuilderExtensions
{
    private static readonly NoopResult _noopHttpResult = new NoopResult();

    /// <summary>
    /// Add endpoints for registering, logging in, and logging out using ASP.NET Core Identity.
    /// </summary>
    /// <typeparam name="TUser">The type describing the user. This should match the generic parameter in <see cref="UserManager{TUser}"/>.</typeparam>
    /// <param name="endpoints">
    /// The <see cref="IEndpointRouteBuilder"/> to add the identity endpoints to.
    /// Call <see cref="EndpointRouteBuilderExtensions.MapGroup(IEndpointRouteBuilder, string)"/> to add a prefix to all the endpoints.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the added endpoints.</returns>
    public static IEndpointConventionBuilder MapIdentityApi<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var routeGroup = endpoints.MapGroup("");

        var timeProvider = endpoints.ServiceProvider.GetRequiredService<TimeProvider>();
        var bearerTokenOptions = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        var emailSender = endpoints.ServiceProvider.GetRequiredService<IEmailSender>();
        var linkGenerator = endpoints.ServiceProvider.GetRequiredService<LinkGenerator>();

        // We'll figure out a unique endpoint name based on the final route pattern during endpoint generation.
        string? confirmEmailEndpointName = null;

        // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
        // https://github.com/dotnet/aspnetcore/issues/47338
        routeGroup.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] RegisterRequest registration, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
            {
                throw new NotSupportedException($"{nameof(MapIdentityApi)} requires a user store with email support.");
            }

            if (confirmEmailEndpointName is null)
            {
                throw new NotSupportedException("No email confirmation endpoint was registered!");
            }

            var emailStore = (IUserEmailStore<TUser>)sp.GetRequiredService<IUserStore<TUser>>();

            var user = new TUser();
            await userManager.SetUserNameAsync(user, registration.Username);
            await emailStore.SetEmailAsync(user, registration.Email, CancellationToken.None);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (result.Succeeded)
            {
                var userId = await userManager.GetUserIdAsync(user);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var confirmEmailUrl = linkGenerator.GetPathByName(confirmEmailEndpointName, new()
                {
                    ["userId"] = userId,
                    ["code"] = code,
                });

                if (confirmEmailUrl is null)
                {
                    throw new NotSupportedException($"Could not find endpoint named '{confirmEmailEndpointName}'.");
                }

                await emailSender.SendEmailAsync(registration.Email, "Confirm your email",
                    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(confirmEmailUrl)}'>clicking here</a>.");

                return TypedResults.Ok();
            }

            return CreateValidationProblem(result);
        });

        routeGroup.MapPost("/login", async Task<Results<Ok<AccessTokenResponse>, ProblemHttpResult, IResult>>
            ([FromBody] LoginRequest login, [FromQuery] bool? cookieMode, [FromQuery] bool? persistCookies, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();

            signInManager.PrimaryAuthenticationScheme = cookieMode == true ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;
            var isPersistent = persistCookies ?? true;

            var result = await signInManager.PasswordSignInAsync(login.Username, login.Password, isPersistent, lockoutOnFailure: true);

            if (result.RequiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(login.TwoFactorCode))
                {
                    result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent, rememberClient: isPersistent);
                }
                else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                {
                    result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
                }
            }

            if (result.Succeeded)
            {
                // The signInManager already produced the needed response in the form of a cookie or bearer token.
                return _noopHttpResult;
            }

            return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
        });

        routeGroup.MapPost("/refresh", async Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>>
            ([FromBody] RefreshRequest refreshRequest, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

            // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
            if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                timeProvider.GetUtcNow() >= expiresUtc ||
                await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not TUser user)

            {
                return TypedResults.Challenge();
            }

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        });

        routeGroup.MapGet("/confirmEmail", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>>
            ([FromQuery] string userId, [FromQuery] string code, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var userManager = signInManager.UserManager;

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                // We could respond with a 404 instead of a 401 like Identity UI, but that feels like unnecessary information.
                return TypedResults.Unauthorized();
            }

            IdentityResult result;
            try
            {
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                result = await userManager.ConfirmEmailAsync(user, code);
            }
            catch (FormatException)
            {
                result = IdentityResult.Failed();
            }

            if (!result.Succeeded)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Text("Thank you for confirming your email.");
        })
        .Add(endpointBuilder =>
        {
            var finalPattern = ((RouteEndpointBuilder)endpointBuilder).RoutePattern.RawText;
            confirmEmailEndpointName = $"{nameof(MapIdentityApi)}-{finalPattern}";
            endpointBuilder.Metadata.Add(new EndpointNameMetadata(confirmEmailEndpointName));
            endpointBuilder.Metadata.Add(new RouteNameMetadata(confirmEmailEndpointName));
        });

        routeGroup.MapPost("/resetPassword", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] ResetPasswordRequest resetRequest, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
            {
                throw new NotSupportedException($"{nameof(MapIdentityApi)} requires a user store with email support.");
            }

            if (resetRequest.ResetCode is not null && resetRequest.NewPassword is null)
            {
                return CreateValidationProblem("MissingNewPassword", "A password reset code was provided without a new password.");
            }

            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is null || !(await userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
                // returned a 400 for an invalid code given a valid user email.
                if (resetRequest.ResetCode is not null)
                {
                    return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken()));
                }
            }
            else if (resetRequest.ResetCode is null)
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                await emailSender.SendEmailAsync(resetRequest.Email, "Reset your password",
                    $"Reset your password using the following code: {HtmlEncoder.Default.Encode(code)}");
            }
            else
            {
                Debug.Assert(resetRequest.NewPassword is not null);

                IdentityResult result;
                try
                {
                    var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.ResetCode));
                    result = await userManager.ResetPasswordAsync(user, code, resetRequest.NewPassword);
                }
                catch (FormatException)
                {
                    result = IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken());
                }

                if (!result.Succeeded)
                {
                    return CreateValidationProblem(result);
                }
            }

            return TypedResults.Ok();
        });

        var accountGroup = routeGroup.MapGroup("/account").RequireAuthorization();

        accountGroup.MapGet("/2fa", async Task<Results<Ok<TwoFactorResponse>, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var userManager = signInManager.UserManager;
            var user = await userManager.GetUserAsync(claimsPrincipal);

            if (user is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(await CreateTwoFactorResponseAsync(user, signInManager));
        });

        accountGroup.MapPost("/2fa", async Task<Results<Ok<TwoFactorResponse>, ValidationProblem, NotFound>>
            (ClaimsPrincipal claimsPrincipal, [FromBody] TwoFactorRequest request, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var userManager = signInManager.UserManager;
            var user = await userManager.GetUserAsync(claimsPrincipal);

            if (user is null)
            {
                return TypedResults.NotFound();
            }

            if (request.Enable == true)
            {
                if (request.ResetSharedKey)
                {
                    return CreateValidationProblem("CannotResetSharedKeyAndEnable",
                        "Resetting the 2fa shared key must disable 2fa until a 2fa token based on the new shared key is validated.");
                }
                else if (request.TwoFactorCode is null)
                {
                    return CreateValidationProblem("RequiresTwoFactor",
                        "No 2fa token was provided by the request. A valid 2fa token is required to enable 2fa.");
                }
                else if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.TwoFactorCode))
                {
                    return CreateValidationProblem("InvalidTwoFactorCode",
                        "The 2fa token provide by the request was invalid. A valid 2fa token is required to enable 2fa.");
                }

                await userManager.SetTwoFactorEnabledAsync(user, true);
            }
            else if (request.Enable == false || request.ResetSharedKey)
            {
                await userManager.SetTwoFactorEnabledAsync(user, false);
            }

            if (request.ResetSharedKey)
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
            }

            string[]? recoveryCodes = null;
            if (request.ResetRecoveryCodes || (request.Enable == true && await userManager.CountRecoveryCodesAsync(user) == 0))
            {
                var recoveryCodesEnumerable = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                recoveryCodes = recoveryCodesEnumerable?.ToArray();
            }

            if (request.ForgetMachine)
            {
                await signInManager.ForgetTwoFactorClientAsync();
            }

            return TypedResults.Ok(await CreateTwoFactorResponseAsync(user, signInManager, recoveryCodes));
        });

        return new IdentityEndpointsConventionBuilder(routeGroup);
    }

    private static ValidationProblem CreateValidationProblem(string errorCode, string errorDescription) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> {
            { errorCode, new[] { errorDescription } }
        });

    private static ValidationProblem CreateValidationProblem(IdentityResult result)
    {
        Debug.Assert(!result.Succeeded);
        return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
    }

    private static async Task<TwoFactorResponse> CreateTwoFactorResponseAsync<TUser>(TUser user, SignInManager<TUser> signInManager, string[]? recoveryCodes = null)
        where TUser : class
    {
        var userManager = signInManager.UserManager;

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        return new TwoFactorResponse
        {
            SharedKey = key!,
            RecoveryCodes = recoveryCodes,
            RecoveryCodesLeft = recoveryCodes?.Length ?? await userManager.CountRecoveryCodesAsync(user),
            IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
        };
    }

    // Wrap RouteGroupBuilder with a non-public type to avoid a potential future behavioral breaking change.
    private sealed class IdentityEndpointsConventionBuilder(RouteGroupBuilder inner) : IEndpointConventionBuilder
    {
        private IEndpointConventionBuilder InnerAsConventionBuilder => inner;

        public void Add(Action<EndpointBuilder> convention) => InnerAsConventionBuilder.Add(convention);
        public void Finally(Action<EndpointBuilder> finallyConvention) => InnerAsConventionBuilder.Finally(finallyConvention);
    }

    private sealed class NoopResult : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromBodyAttribute : Attribute, IFromBodyMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromServicesAttribute : Attribute, IFromServiceMetadata
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    private sealed class FromQueryAttribute : Attribute, IFromQueryMetadata
    {
        public string? Name => null;
    }
}

