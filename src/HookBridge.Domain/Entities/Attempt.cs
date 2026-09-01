using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class Attempt : Entity<Guid>, ITenantScoped
{
    public Guid DeliveryId { get; private set; }
    public Guid TenantId { get; private set; }
    public int AttemptNumber { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string RequestHeadersJson { get; private set; } = string.Empty;
    public string RequestBody { get; private set; } = string.Empty;
    public string? ResponseHeadersJson { get; private set; }
    public string? ResponseBody { get; private set; }
    public long ElapsedMs { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset ExecutedAt { get; private set; }

    private Attempt() { }

    public static Result<Attempt> Create(
        Guid deliveryId,
        Guid tenantId,
        int attemptNumber,
        int? httpStatusCode,
        string requestHeadersJson,
        string requestBody,
        string? responseHeadersJson,
        string? responseBody,
        long elapsedMs,
        string? errorMessage,
        DateTimeOffset executedAt)
    {
        if (deliveryId == Guid.Empty || tenantId == Guid.Empty)
        {
            return Result.Failure<Attempt>(DomainError.Validation("Attempt.InvalidScope", "DeliveryId and TenantId are required."));
        }

        return Result.Success(new Attempt
        {
            Id = Guid.NewGuid(),
            DeliveryId = deliveryId,
            TenantId = tenantId,
            AttemptNumber = attemptNumber,
            HttpStatusCode = httpStatusCode,
            RequestHeadersJson = requestHeadersJson,
            RequestBody = requestBody,
            ResponseHeadersJson = responseHeadersJson,
            ResponseBody = responseBody,
            ElapsedMs = elapsedMs,
            ErrorMessage = errorMessage,
            ExecutedAt = executedAt
        });
    }
}
