using HookBridge.Domain.Common;

namespace HookBridge.Domain.Entities;

public sealed class SandboxRequest : Entity<Guid>, ITenantScoped
{
    public Guid SandboxId { get; private set; }
    public Guid TenantId { get; private set; }
    public string HttpMethod { get; private set; } = "POST";
    public string Path { get; private set; } = string.Empty;
    public string? QueryString { get; private set; }
    public string HeadersJson { get; private set; } = "{}";
    public string? Body { get; private set; }
    public string? ContentType { get; private set; }
    public long ContentLength { get; private set; }
    public string? ClientIp { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public int ResponseDelayMs { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public double DurationMs { get; private set; }

    public WebhookSandbox? Sandbox { get; private set; }

    private SandboxRequest() { }

    public static Result<SandboxRequest> Create(
        Guid sandboxId,
        Guid tenantId,
        string httpMethod,
        string path,
        string? queryString,
        string headersJson,
        string? body,
        string? contentType,
        long contentLength,
        string? clientIp,
        int responseStatusCode,
        int responseDelayMs,
        DateTimeOffset receivedAt,
        double durationMs)
    {
        if (sandboxId == Guid.Empty)
        {
            return Result.Failure<SandboxRequest>(DomainError.Validation("SandboxRequest.InvalidSandboxId", "SandboxId cannot be empty."));
        }

        if (tenantId == Guid.Empty)
        {
            return Result.Failure<SandboxRequest>(DomainError.Validation("SandboxRequest.InvalidTenantId", "TenantId cannot be empty."));
        }

        return Result.Success(new SandboxRequest
        {
            Id = Guid.NewGuid(),
            SandboxId = sandboxId,
            TenantId = tenantId,
            HttpMethod = string.IsNullOrWhiteSpace(httpMethod) ? "POST" : httpMethod.ToUpperInvariant(),
            Path = path ?? string.Empty,
            QueryString = queryString,
            HeadersJson = string.IsNullOrWhiteSpace(headersJson) ? "{}" : headersJson,
            Body = body,
            ContentType = contentType,
            ContentLength = Math.Max(0, contentLength),
            ClientIp = clientIp,
            ResponseStatusCode = responseStatusCode,
            ResponseDelayMs = Math.Max(0, responseDelayMs),
            ReceivedAt = receivedAt,
            DurationMs = Math.Max(0, durationMs)
        });
    }
}
