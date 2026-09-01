namespace HookBridge.Domain.Common;

/// <summary>
/// Marker interface tracking entity creation and modification timestamps in UTC.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
}
