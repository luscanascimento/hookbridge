using HookBridge.Domain.Entities;

namespace HookBridge.Application.Abstractions;

public sealed record GeneratedTokens(
    string AccessToken,
    string RefreshToken,
    string RefreshTokenHash,
    int ExpiresInSeconds);

public interface ITokenService
{
    GeneratedTokens GenerateTokens(User user, Tenant tenant);
    string HashToken(string token);
}
