namespace HookBridge.Domain.Common;

/// <summary>
/// Immutable representation of an operational or business rule failure with RFC 7807 metadata.
/// </summary>
public sealed record DomainError(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static readonly DomainError None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static DomainError Validation(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Validation, validationErrors, extensions);

    public static DomainError NotFound(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.NotFound, null, extensions);

    public static DomainError Conflict(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Conflict, null, extensions);

    public static DomainError Unauthorized(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Unauthorized, null, extensions);

    public static DomainError Forbidden(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Forbidden, null, extensions);

    public static DomainError Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Failure, null, extensions);

    public static DomainError Unexpected(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Unexpected, null, extensions);
}
