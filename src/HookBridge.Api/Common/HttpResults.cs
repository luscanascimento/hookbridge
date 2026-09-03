using HookBridge.Domain.Common;

namespace HookBridge.Api.Common;

public static class HttpResults
{
    public static IResult Match<TValue>(Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(string.Empty, result.Value),
                StatusCodes.Status202Accepted => Results.Accepted(string.Empty, result.Value),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok(result.Value)
            };
        }

        return ToProblem(result.Error);
    }

    public static IResult Match(Result result, int successStatusCode = StatusCodes.Status204NoContent)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status200OK => Results.Ok(),
                StatusCodes.Status201Created => Results.Created(),
                StatusCodes.Status202Accepted => Results.Accepted(),
                _ => Results.NoContent()
            };
        }

        return ToProblem(result.Error);
    }

    private static IResult ToProblem(DomainError error)
    {
        return error.Type switch
        {
            ErrorType.Validation => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failure",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),

            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),

            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),

            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code }),

            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Failure",
                detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code })
        };
    }
}
