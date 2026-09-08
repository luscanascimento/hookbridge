namespace HookBridge.Application.ControlPlane.UseCases.Sandbox;

public sealed record WebhookSandboxResponse(
    Guid Id,
    string Name,
    string Slug,
    string ReceiverUrl,
    int DefaultResponseStatusCode,
    string? DefaultResponseBody,
    string DefaultResponseContentType,
    int DefaultResponseDelayMs,
    bool IsActive,
    DateTimeOffset? ExpiresAt,
    int TotalRequestsCount,
    DateTimeOffset? LastRequestAt,
    DateTimeOffset CreatedAt
);

public sealed record CreateSandboxCommand(
    string Name,
    string? CustomSlug = null,
    int? DefaultStatusCode = 200,
    string? DefaultBody = "{\"status\": \"ok\", \"received\": true}",
    string? DefaultContentType = "application/json",
    int? DefaultDelayMs = 0,
    int? TtlHours = null
);

public sealed record UpdateSandboxConfigCommand(
    string Name,
    int DefaultStatusCode,
    string? DefaultBody,
    string DefaultContentType,
    int DefaultDelayMs,
    bool IsActive
);

public sealed record SandboxRequestResponse(
    Guid Id,
    Guid SandboxId,
    string HttpMethod,
    string Path,
    string? QueryString,
    string HeadersJson,
    string? Body,
    string? ContentType,
    long ContentLength,
    string? ClientIp,
    int ResponseStatusCode,
    int ResponseDelayMs,
    DateTimeOffset ReceivedAt,
    double DurationMs
);

public sealed record SandboxRequestSummaryResponse(
    Guid Id,
    Guid SandboxId,
    string HttpMethod,
    string Path,
    string? ContentType,
    long ContentLength,
    string? ClientIp,
    int ResponseStatusCode,
    int ResponseDelayMs,
    DateTimeOffset ReceivedAt,
    double DurationMs
);

public sealed record PagedSandboxRequestsResponse(
    IReadOnlyList<SandboxRequestSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record RealtimeSandboxEvent(
    string EventType,
    Guid TenantId,
    Guid SandboxId,
    string SandboxSlug,
    SandboxRequestResponse Request,
    DateTimeOffset Timestamp
);

public sealed record ProcessSandboxRequestResult(
    int StatusCode,
    string ContentType,
    string? Body,
    int DelayMs
);
