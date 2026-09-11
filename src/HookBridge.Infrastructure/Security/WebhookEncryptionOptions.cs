using System.ComponentModel.DataAnnotations;

namespace HookBridge.Infrastructure.Security;

public sealed class WebhookEncryptionOptions
{
    public const string SectionName = "WebhookEncryption";

    /// <summary>
    /// 256-bit Master Key for AES-GCM encryption of webhook secrets at rest.
    /// Can be provided as a 64-character hex string or at least 32-character key.
    /// </summary>
    [Required(ErrorMessage = "WebhookEncryption:MasterKey is required.")]
    [MinLength(32, ErrorMessage = "WebhookEncryption:MasterKey must be at least 32 characters or a 64-character hex string.")]
    public string MasterKey { get; set; } = string.Empty;
}
