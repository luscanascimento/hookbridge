using HookBridge.Domain.Common;
using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

public sealed class Endpoint : AggregateRoot<Guid>, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public EndpointStatus Status { get; private set; }
    public string? DisabledReason { get; private set; }
    public int RateLimitPerMinute { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ICollection<WebhookSecret> Secrets { get; private set; } = new List<WebhookSecret>();
    public ICollection<Subscription> Subscriptions { get; private set; } = new List<Subscription>();
    public ICollection<Delivery> Deliveries { get; private set; } = new List<Delivery>();

    private Endpoint() { }

    public static Result<Endpoint> Create(
        Guid tenantId,
        Guid applicationId,
        string targetUrl,
        string? description,
        DateTimeOffset now,
        int rateLimitPerMinute = 600,
        int timeoutSeconds = 10)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<Endpoint>(DomainError.Validation("Endpoint.InvalidTenantId", "TenantId cannot be empty."));
        }

        if (applicationId == Guid.Empty)
        {
            return Result.Failure<Endpoint>(DomainError.Validation("Endpoint.InvalidApplicationId", "ApplicationId cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<Endpoint>(DomainError.Validation("Endpoint.InvalidUrl", "A valid HTTP/HTTPS URL is required."));
        }

        return Result.Success(new Endpoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            TargetUrl = targetUrl.Trim(),
            Description = description?.Trim(),
            Status = EndpointStatus.Active,
            RateLimitPerMinute = rateLimitPerMinute,
            TimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 30),
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    public void UpdateUrl(string targetUrl, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);
        TargetUrl = targetUrl.Trim();
        UpdatedAt = now;
    }

    public void SetStatus(EndpointStatus status, string? reason, DateTimeOffset now)
    {
        Status = status;
        DisabledReason = reason?.Trim();
        UpdatedAt = now;
    }
}
