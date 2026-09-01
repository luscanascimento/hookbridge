namespace HookBridge.Domain.Common;

/// <summary>
/// Categorization of domain and application errors for RFC 7807 HTTP ProblemDetails mapping.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Unexpected = 6
}
