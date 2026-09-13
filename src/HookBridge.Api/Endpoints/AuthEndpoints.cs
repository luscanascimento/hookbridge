using HookBridge.Api.Common;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class AuthEndpoints
{
    private static void SetAuthCookies(HttpContext context, AuthResponse auth)
    {
        context.Response.Cookies.Append("hb_access_token", auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api",
            MaxAge = TimeSpan.FromMinutes(15)
        });

        context.Response.Cookies.Append("hb_refresh_token", auth.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth/refresh",
            MaxAge = TimeSpan.FromDays(7)
        });
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth-policy");

        // 1. Register new Tenant + Admin
        group.MapPost("/register", async (
            HttpContext httpContext,
            [FromBody] RegisterTenantCommand command,
            [FromServices] RegisterTenantUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            if (!result.IsSuccess) return HttpResults.ToProblem(result.Error);
            SetAuthCookies(httpContext, result.Value);
            return Results.Created($"/api/v1/users/{result.Value.User.UserId}", result.Value);
        })
        .WithName("RegisterTenant")
        .WithSummary("Registers a new tenant organization and provisions the initial TenantAdmin user.")
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 2. User Login
        group.MapPost("/login", async (
            HttpContext httpContext,
            [FromBody] LoginCommand command,
            [FromServices] LoginUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            if (!result.IsSuccess) return HttpResults.ToProblem(result.Error);
            SetAuthCookies(httpContext, result.Value);
            return Results.Ok(result.Value);
        })
        .WithName("Login")
        .WithSummary("Authenticates user credentials and returns JWT access and refresh tokens.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        // 3. Refresh Token Rotation
        group.MapPost("/refresh", async (
            HttpContext httpContext,
            [FromBody] RefreshTokenCommand? command,
            [FromServices] RefreshTokenUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var refreshToken = command?.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                httpContext.Request.Cookies.TryGetValue("hb_refresh_token", out refreshToken);
            }
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Problem(
                    detail: "Refresh token is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await useCase.ExecuteAsync(new RefreshTokenCommand(refreshToken), cancellationToken);
            if (!result.IsSuccess) return HttpResults.ToProblem(result.Error);
            SetAuthCookies(httpContext, result.Value);
            return Results.Ok(result.Value);
        })
        .WithName("RefreshToken")
        .WithSummary("Rotates the active refresh token and returns a fresh JWT access and refresh token pair.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // 4. Logout & Revoke Session
        group.MapPost("/logout", async (
            HttpContext httpContext,
            [FromBody] LogoutCommand command,
            [FromServices] LogoutUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            httpContext.Response.Cookies.Delete("hb_access_token", new CookieOptions { Path = "/api" });
            httpContext.Response.Cookies.Delete("hb_refresh_token", new CookieOptions { Path = "/api/v1/auth/refresh" });
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("Logout")
        .WithSummary("Terminates active session and revokes refresh tokens.")
        .Produces<bool>(StatusCodes.Status200OK);

        // 5. Current User Profile
        group.MapGet("/me", async (
            [FromServices] GetCurrentUserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status200OK);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Retrieves identity, role, and tenant metadata for the currently authenticated user.")
        .RequireAuthorization()
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // 6. Invite User (TenantAdmin only)
        group.MapPost("/invite", async (
            [FromBody] InviteUserCommand command,
            [FromServices] InviteUserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return HttpResults.Match(result, StatusCodes.Status201Created);
        })
        .WithName("InviteUser")
        .WithSummary("Provisions a new user within the current tenant boundary with the specified role.")
        .RequireAuthorization(AuthorizationPolicies.RequireTenantAdmin)
        .Produces<UserProfileResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return app;
    }
}
