using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;

namespace HookBridge.Infrastructure.Security;

public sealed class WebhookSigner : IWebhookSigner
{
    private const int MaxFutureDriftSeconds = 60; // Max allowed future timestamp clock drift

    public string ComputeHmacSha256(string rawPayload, string secretKey, long unixTimestamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(secretKey);
        rawPayload ??= string.Empty;

        Span<byte> hashBytes = stackalloc byte[32];
        ComputeHmacSha256Bytes(rawPayload, secretKey, unixTimestamp, hashBytes);
        return Convert.ToHexStringLower(hashBytes);
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

        // 3. Validate anti-replay tolerance window and future drift
        var currentEpoch = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var allowedTolerance = (long)(tolerance?.TotalSeconds ?? IWebhookSigner.DefaultToleranceSeconds);

        // Reject excessive future timestamps (future replay spoofing)
        if (timestamp > currentEpoch + MaxFutureDriftSeconds)
        {
            return Result.Failure<bool>(DomainError.Validation(
                "Signature.FutureTimestamp",
                $"Timestamp {timestamp} is too far in the future (current: {currentEpoch}). Clock drift exceeds {MaxFutureDriftSeconds}s."));
        }

        // Reject expired timestamps (past replay attacks)
        var age = currentEpoch - timestamp;
        if (age > allowedTolerance)
        {
            return Result.Failure<bool>(DomainError.Validation(
                "Signature.TimestampOutOfTolerance",
                $"Timestamp {timestamp} is expired. Webhook signature age is {age}s, exceeding maximum tolerance window of {allowedTolerance}s."));
        }

        // 4. Constant-time verification against candidate secrets
        rawPayload ??= string.Empty;
        var matched = false;

        Span<byte> expectedBytes = stackalloc byte[64];
        Span<byte> receivedBytes = stackalloc byte[64];

        foreach (var secret in secrets)
        {
            var expectedSignature = ComputeHmacSha256(rawPayload, secret, timestamp);
            Encoding.UTF8.GetBytes(expectedSignature, expectedBytes);

            foreach (var receivedSig in signatures)
            {
                if (receivedSig.Length == 64)
                {
                    Encoding.UTF8.GetBytes(receivedSig, receivedBytes);
                    if (CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes))
                    {
                        matched = true;
                        break;
                    }
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

    private static void ComputeHmacSha256Bytes(string rawPayload, string secretKey, long unixTimestamp, Span<byte> destination)
    {
        var keyByteCount = Encoding.UTF8.GetByteCount(secretKey);
        byte[]? rentedKey = null;
        Span<byte> keySpan = keyByteCount <= 256
            ? stackalloc byte[keyByteCount]
            : (rentedKey = ArrayPool<byte>.Shared.Rent(keyByteCount)).AsSpan(0, keyByteCount);

        try
        {
            Encoding.UTF8.GetBytes(secretKey, keySpan);

            Span<byte> tsBuffer = stackalloc byte[32];
            if (!Utf8Formatter.TryFormat(unixTimestamp, tsBuffer, out var tsWritten))
            {
                tsWritten = Encoding.UTF8.GetBytes(unixTimestamp.ToString(CultureInfo.InvariantCulture), tsBuffer);
            }

            var rawByteCount = Encoding.UTF8.GetByteCount(rawPayload);
            var totalPayloadBytes = tsWritten + 1 + rawByteCount;

            byte[]? rentedPayload = null;
            Span<byte> payloadSpan = totalPayloadBytes <= 1024
                ? stackalloc byte[totalPayloadBytes]
                : (rentedPayload = ArrayPool<byte>.Shared.Rent(totalPayloadBytes)).AsSpan(0, totalPayloadBytes);

            try
            {
                tsBuffer[..tsWritten].CopyTo(payloadSpan);
                payloadSpan[tsWritten] = (byte)'.';
                if (rawByteCount > 0)
                {
                    Encoding.UTF8.GetBytes(rawPayload, payloadSpan[(tsWritten + 1)..]);
                }

                HMACSHA256.HashData(keySpan, payloadSpan, destination);
            }
            finally
            {
                if (rentedPayload != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedPayload);
                }
            }
        }
        finally
        {
            if (rentedKey != null)
            {
                ArrayPool<byte>.Shared.Return(rentedKey);
            }
        }
    }
}
