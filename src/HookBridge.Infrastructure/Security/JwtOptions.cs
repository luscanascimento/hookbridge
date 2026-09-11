using System.ComponentModel.DataAnnotations;

namespace HookBridge.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Issuer is required.")]
    public string Issuer { get; set; } = "HookBridge.ControlPlane";

    [Required(ErrorMessage = "Jwt:Audience is required.")]
    public string Audience { get; set; } = "HookBridge.DeveloperPortal";

    [Required(ErrorMessage = "Jwt:SecretKey is required.")]
    [MinLength(32, ErrorMessage = "Jwt:SecretKey must be at least 32 characters (256 bits) long.")]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "Jwt:AccessTokenExpirationMinutes must be between 1 and 1440 minutes.")]
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    [Range(1, 365, ErrorMessage = "Jwt:RefreshTokenExpirationDays must be between 1 and 365 days.")]
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
