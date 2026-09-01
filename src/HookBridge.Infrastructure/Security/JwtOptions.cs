namespace HookBridge.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "HookBridge.ControlPlane";
    public string Audience { get; set; } = "HookBridge.DeveloperPortal";
    public string SecretKey { get; set; } = "HookBridge_Super_Secret_Development_Key_2026_Must_Be_At_Least_256_Bits!";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
