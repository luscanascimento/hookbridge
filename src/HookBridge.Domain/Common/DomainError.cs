namespace HookBridge.Domain.Common;

/// <summary>
/// Immutable representation of an operational or business rule failure.
/// </summary>
public sealed record DomainError(string Code, string Message, ErrorType Type)
{
    public static readonly DomainError None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static DomainError Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static DomainError NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static DomainError Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static DomainError Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized);

    public static DomainError Forbidden(string code, string message) =>
        new(code, message, ErrorType.Forbidden);

    public static DomainError Failure(string code, string message) =>
        new(code, message, ErrorType.Failure);

    public static DomainError Unexpected(string code, string message) =>
        new(code, message, ErrorType.Unexpected);
}
