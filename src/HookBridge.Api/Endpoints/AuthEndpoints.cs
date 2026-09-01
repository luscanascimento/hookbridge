using System.Security.Claims;
using HookBridge.Application.Auth.DTOs;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // 1. Register new Tenant + Admin
        group.MapPost("/register", async (
            [FromBody] RegisterTenantCommand command,
            [FromServices] RegisterTenantUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return ToHttpResult(result, StatusCodes.Status201Created);
        })
        .WithName("RegisterTenant")
        .WithSummary("Registers a new tenant organization and provisions the initial TenantAdmin user.")
        .Produces<AuthResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // 2. User Login
        group.MapPost("/login", async (
            [FromBody] LoginCommand command,
            [FromServices] LoginUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("Login")
        .WithSummary("Authenticates user credentials and returns JWT access and refresh tokens.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        // 3. Refresh Token Rotation
        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenCommand command,
            [FromServices] RefreshTokenUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("RefreshToken")
        .WithSummary("Rotates the active refresh token and returns a fresh JWT access and refresh token pair.")
        .Produces<AuthResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // 4. Current User Profile
        group.MapGet("/me", async (
            [FromServices] GetCurrentUserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(cancellationToken);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Retrieves identity, role, and tenant metadata for the currently authenticated user.")
        .RequireAuthorization()
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        // 5. Invite User (TenantAdmin only)
        group.MapPost("/invite", async (
            [FromBody] InviteUserCommand command,
            [FromServices] InviteUserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(command, cancellationToken);
            return ToHttpResult(result, StatusCodes.Status201Created);
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

    private static IResult ToHttpResult<TValue>(Result<TValue> result, int successStatusCode)
    {
        if (result.IsSuccess)
        {
            return successStatusCode == StatusCodes.Status201Created
                ? Results.Created(string.Empty, result.Value)
                : Results.Ok(result.Value);
        }

        return result.Error.Type switch
        {
            ErrorType.Validation => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failure",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Failure",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code })
        };
    }
}
