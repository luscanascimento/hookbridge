using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Security;

public sealed class AesSecretEncryptor : ISecretEncryptor
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private readonly byte[] _key;

    public AesSecretEncryptor(IOptions<WebhookEncryptionOptions> options)
    {
        var rawKey = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new ArgumentException("WebhookEncryption:MasterKey must be provided.", nameof(options));
        }

        if (rawKey.Length == 64)
        {
            _key = Convert.FromHexString(rawKey);
        }
        else
        {
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        }
    }

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combined payload: [Nonce(12) | Tag(16) | Ciphertext(N)]
        var result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrEmpty(cipherText);

        var payload = Convert.FromBase64String(cipherText);
        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Ciphertext payload is invalid or truncated.");
        }

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var cipherLength = payload.Length - NonceSizeBytes - TagSizeBytes;
        var cipherBytes = new byte[cipherLength];

        Buffer.BlockCopy(payload, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(payload, NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(payload, NonceSizeBytes + TagSizeBytes, cipherBytes, 0, cipherLength);

        var plainBytes = new byte[cipherLength];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
