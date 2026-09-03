using System.Security.Cryptography;
using FluentAssertions;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace HookBridge.UnitTests.Security;

public class AesSecretEncryptorTests
{
    private readonly AesSecretEncryptor _encryptor;

    public AesSecretEncryptorTests()
    {
        var options = Options.Create(new WebhookEncryptionOptions
        {
            MasterKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        });
        _encryptor = new AesSecretEncryptor(options);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldRestoreOriginalPlainText()
    {
        // Arrange
        var original = "whsec_super_secret_webhook_signing_key_2026";

        // Act
        var cipherText = _encryptor.Encrypt(original);
        var decrypted = _encryptor.Decrypt(cipherText);

        // Assert
        cipherText.Should().NotBeNullOrWhiteSpace();
        cipherText.Should().NotBe(original);
        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_CalledTwiceOnSameInput_ShouldProduceDistinctCipherTextsDueToUniqueNonce()
    {
        // Arrange
        var text = "whsec_test_payload_12345";

        // Act
        var cipher1 = _encryptor.Encrypt(text);
        var cipher2 = _encryptor.Encrypt(text);

        // Assert
        cipher1.Should().NotBe(cipher2);
        _encryptor.Decrypt(cipher1).Should().Be(text);
        _encryptor.Decrypt(cipher2).Should().Be(text);
    }

    [Fact]
    public void Decrypt_WithTamperedPayload_ShouldThrowCryptographicException()
    {
        // Arrange
        var cipherText = _encryptor.Encrypt("secret_value");
        var rawBytes = Convert.FromBase64String(cipherText);
        rawBytes[^1] ^= 0xFF; // Flip last byte
        var tamperedCipher = Convert.ToBase64String(rawBytes);

        // Act & Assert
        var act = () => _encryptor.Decrypt(tamperedCipher);
        act.Should().Throw<AuthenticationTagMismatchException>();
    }
}
