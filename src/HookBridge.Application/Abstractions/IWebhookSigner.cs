using HookBridge.Domain.Common;

namespace HookBridge.Application.Abstractions;

/// <summary>
/// Provides cryptographic HMAC-SHA256 signature generation, formatting, and constant-time verification
/// for webhook payloads with anti-replay timestamp protection and secret rotation tolerance.
/// </summary>
public interface IWebhookSigner
{
    public const string HeaderName = "X-HookBridge-Signature";
    public const int DefaultToleranceSeconds = 300;

    /// <summary>
    /// Computes the HMAC-SHA256 signature for the raw payload using the provided secret key.
    /// </summary>
    string ComputeHmacSha256(string rawPayload, string secretKey, long unixTimestamp);

    /// <summary>
    /// Generates the standard X-HookBridge-Signature header value (e.g. t=1700000000,v1=hexSig).
    /// </summary>
    string GenerateSignatureHeader(string rawPayload, string secretKey, DateTimeOffset timestamp);

    /// <summary>
    /// Generates the standard X-HookBridge-Signature header value with multiple signatures during secret rotation.
    /// </summary>
    string GenerateSignatureHeader(string rawPayload, IEnumerable<string> secretKeys, DateTimeOffset timestamp);

    /// <summary>
    /// Verifies the X-HookBridge-Signature header against candidate secrets with anti-replay tolerance window.
    /// </summary>
    Result<bool> VerifySignature(
        string rawPayload,
        string signatureHeader,
        IEnumerable<string> candidateSecrets,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null);

    /// <summary>
    /// Verifies the X-HookBridge-Signature header against a single secret with anti-replay tolerance window.
    /// </summary>
    Result<bool> VerifySignature(
        string rawPayload,
        string signatureHeader,
        string secretKey,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null);
}
