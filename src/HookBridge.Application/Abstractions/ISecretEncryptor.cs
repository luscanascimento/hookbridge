namespace HookBridge.Application.Abstractions;

/// <summary>
/// Provides authenticated symmetric encryption and decryption for sensitive secrets stored at rest.
/// </summary>
public interface ISecretEncryptor
{
    /// <summary>
    /// Encrypts plaintext into a base64 encoded ciphertext payload with authenticated tag and nonce.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts a base64 encoded ciphertext payload back to plaintext.
    /// </summary>
    string Decrypt(string cipherText);
}
