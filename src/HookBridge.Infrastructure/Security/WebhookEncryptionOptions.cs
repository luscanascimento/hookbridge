namespace HookBridge.Infrastructure.Security;

public sealed class WebhookEncryptionOptions
{
    public const string SectionName = "WebhookEncryption";

    /// <summary>
    /// 256-bit Master Key for AES-GCM encryption of webhook secrets at rest.
    /// Default development key is provided for out-of-the-box local developer workflows.
    /// </summary>
    public string MasterKey { get; set; } = "7f8e9d0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6";
}
