namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record GenerateSignatureCommand(
    string Payload,
    DateTimeOffset? Timestamp = null);

public sealed record GenerateSignatureResponse(
    string SignatureHeader,
    long Timestamp,
    IReadOnlyList<string> Signatures,
    int SecretsCount);

public sealed record VerifySignatureCommand(
    string Payload,
    string SignatureHeader,
    int ToleranceSeconds = 300);

public sealed record VerifySignatureResponse(
    bool IsValid,
    string Status,
    string? ErrorMessage);
