using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;

namespace HookBridge.Infrastructure.Security;

public sealed class WebhookSigner : IWebhookSigner
{
    public string ComputeHmacSha256(string rawPayload, string secretKey, long unixTimestamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(secretKey);
        rawPayload ??= string.Empty;

        var canonicalPayload = $"{unixTimestamp}.{rawPayload}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public string GenerateSignatureHeader(string rawPayload, string secretKey, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(secretKey);
        var unixTimestamp = timestamp.ToUnixTimeSeconds();
        var signature = ComputeHmacSha256(rawPayload, secretKey, unixTimestamp);
        return $"t={unixTimestamp},v1={signature}";
    }

    public string GenerateSignatureHeader(string rawPayload, IEnumerable<string> secretKeys, DateTimeOffset timestamp)
    {
        var keysList = secretKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList()
            ?? throw new ArgumentException("At least one secret key must be provided.", nameof(secretKeys));

        if (keysList.Count == 0)
        {
            throw new ArgumentException("At least one secret key must be provided.", nameof(secretKeys));
        }

        var unixTimestamp = timestamp.ToUnixTimeSeconds();
        var parts = new List<string>(keysList.Count + 1)
        {
            $"t={unixTimestamp}"
        };

        foreach (var key in keysList)
        {
            var signature = ComputeHmacSha256(rawPayload, key, unixTimestamp);
            parts.Add($"v1={signature}");
        }

        return string.Join(",", parts);
    }

    public Result<bool> VerifySignature(
        string rawPayload,
        string signatureHeader,
        IEnumerable<string> candidateSecrets,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.EmptyHeader", "Signature header cannot be empty."));
        }

        var secrets = candidateSecrets?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (secrets == null || secrets.Count == 0)
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.NoCandidateSecrets", "At least one candidate secret must be supplied for verification."));
        }

        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 1. Extract and validate timestamp
        var tPart = parts.FirstOrDefault(p => p.StartsWith("t=", StringComparison.OrdinalIgnoreCase));
        if (tPart == null || !long.TryParse(tPart.AsSpan(2), out var timestamp))
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.MalformedHeader", "Missing or invalid timestamp in signature header."));
        }

        // 2. Extract v1 signatures
        var signatures = parts
            .Where(p => p.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[3..])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (signatures.Count == 0)
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.MissingSignatures", "No v1 signature schemes found in header."));
        }

        // 3. Validate anti-replay tolerance window
        var currentEpoch = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var allowedTolerance = (long)(tolerance?.TotalSeconds ?? IWebhookSigner.DefaultToleranceSeconds);
        var diff = Math.Abs(currentEpoch - timestamp);

        if (diff > allowedTolerance)
        {
            return Result.Failure<bool>(DomainError.Validation(
                "Signature.TimestampOutOfTolerance",
                $"Timestamp {timestamp} is outside the allowed tolerance window of {allowedTolerance} seconds (difference: {diff}s)."));
        }

        // 4. Constant-time verification against candidate secrets
        rawPayload ??= string.Empty;
        var matched = false;

        foreach (var secret in secrets)
        {
            var expectedSignature = ComputeHmacSha256(rawPayload, secret, timestamp);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

            foreach (var receivedSig in signatures)
            {
                var receivedBytes = Encoding.UTF8.GetBytes(receivedSig);
                if (CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes))
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                break;
            }
        }

        if (!matched)
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.Invalid", "Webhook HMAC-SHA256 signature verification failed."));
        }

        return Result.Success(true);
    }

    public Result<bool> VerifySignature(
        string rawPayload,
        string signatureHeader,
        string secretKey,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result.Failure<bool>(DomainError.Validation("Signature.EmptySecret", "Secret key cannot be empty."));
        }

        return VerifySignature(rawPayload, signatureHeader, [secretKey], tolerance, now);
    }
}
