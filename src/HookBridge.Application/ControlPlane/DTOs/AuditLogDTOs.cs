namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record AuditLogQuery(
    string? ResourceType = null,
    string? Action = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditEntryResponse(
    Guid Id,
    Guid TenantId,
    Guid? UserId,
    string Action,
    string ResourceType,
    string ResourceId,
    string DetailsJson,
    string? IpAddress,
    string? TraceId,
    DateTimeOffset Timestamp);

public sealed record PagedList<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
