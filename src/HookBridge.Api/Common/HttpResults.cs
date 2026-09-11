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

    public static IResult ToProblem(DomainError error)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code
        };

        if (error.Extensions != null)
        {
            foreach (var (k, v) in error.Extensions)
            {
                extensions[k] = v;
            }
        }

        if (error.ValidationErrors != null && error.ValidationErrors.Count > 0)
        {
            extensions["errors"] = error.ValidationErrors;
        }

        return error.Type switch
        {
            ErrorType.Validation => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failure",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions),

            ErrorType.Unauthorized => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions),

            ErrorType.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions),

            ErrorType.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions),

            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions),

            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Failure",
                detail: error.Message,
                type: "https://tools.ietf.org/html/rfc7807#section-3.1",
                extensions: extensions)
        };
    }
}
