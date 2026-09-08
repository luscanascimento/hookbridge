using System.Security.Cryptography;
using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class WebhookSandbox : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int DefaultResponseStatusCode { get; private set; } = 200;
    public string? DefaultResponseBody { get; private set; } = "{\"status\": \"ok\", \"received\": true}";
    public string DefaultResponseContentType { get; private set; } = "application/json";
    public int DefaultResponseDelayMs { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<SandboxRequest> Requests { get; private set; } = new List<SandboxRequest>();

    private WebhookSandbox() { }

    public static Result<WebhookSandbox> Create(
        Guid tenantId,
        string name,
        string? customSlug,
        DateTimeOffset now,
        int defaultStatusCode = 200,
        string? defaultBody = "{\"status\": \"ok\", \"received\": true}",
        string defaultContentType = "application/json",
        int defaultDelayMs = 0,
        DateTimeOffset? expiresAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<WebhookSandbox>(DomainError.Validation("WebhookSandbox.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<WebhookSandbox>(DomainError.Validation("WebhookSandbox.InvalidName", "Sandbox name cannot be empty."));
        }

        var slug = string.IsNullOrWhiteSpace(customSlug)
            ? GenerateRandomSlug()
            : SanitizeSlug(customSlug);

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 4)
        {
            return Result.Failure<WebhookSandbox>(DomainError.Validation("WebhookSandbox.InvalidSlug", "Sandbox slug must be at least 4 alphanumeric characters."));
        }

        return Result.Success(new WebhookSandbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = slug,
            DefaultResponseStatusCode = Math.Clamp(defaultStatusCode, 100, 599),
            DefaultResponseBody = defaultBody,
            DefaultResponseContentType = string.IsNullOrWhiteSpace(defaultContentType) ? "application/json" : defaultContentType.Trim(),
            DefaultResponseDelayMs = Math.Clamp(defaultDelayMs, 0, 10000),
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public Result UpdateConfig(
        string name,
        int defaultStatusCode,
        string? defaultBody,
        string defaultContentType,
        int defaultDelayMs,
        bool isActive,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DomainError.Validation("WebhookSandbox.InvalidName", "Sandbox name cannot be empty."));
        }

        Name = name.Trim();
        DefaultResponseStatusCode = Math.Clamp(defaultStatusCode, 100, 599);
        DefaultResponseBody = defaultBody;
        DefaultResponseContentType = string.IsNullOrWhiteSpace(defaultContentType) ? "application/json" : defaultContentType.Trim();
        DefaultResponseDelayMs = Math.Clamp(defaultDelayMs, 0, 10000);
        IsActive = isActive;
        UpdatedAt = now;

        return Result.Success();
    }

    private static string GenerateRandomSlug()
    {
        var bytes = RandomNumberGenerator.GetBytes(9);
        return $"sb_{Convert.ToHexStringLower(bytes)}";
    }

    private static string SanitizeSlug(string slug)
    {
        var cleaned = new string(slug.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return cleaned.StartsWith("sb_", StringComparison.OrdinalIgnoreCase) ? cleaned : $"sb_{cleaned}";
    }
}
