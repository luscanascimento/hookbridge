namespace HookBridge.Application.Abstractions;

/// <summary>
/// Provides cryptographic generation, formatting, and hashing for API Keys and Webhook Secrets.
/// </summary>
public interface IApiKeyGenerator
{
    /// <summary>
    /// Generates a cryptographically strong API Key with standard prefix (e.g. hb_live_... or hb_test_...).
    /// </summary>
    (string PlainKey, string KeyPrefix, string KeyHash) GenerateApiKey(string environment = "live");

    /// <summary>
    /// Generates a cryptographically strong Webhook Secret with standard prefix (whsec_...).
    /// </summary>
    (string PlainSecret, string SecretPrefix, string SecretHash) GenerateWebhookSecret();

    /// <summary>
    /// Computes a SHA-256 hash formatted as a lowercase hex string.
    /// </summary>
    string ComputeHash(string input);
}
