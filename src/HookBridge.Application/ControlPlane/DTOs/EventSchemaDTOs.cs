using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record CreateEventSchemaRequest(
    string EventType,
    string Name,
    string? Description,
    SchemaCompatibilityMode CompatibilityMode,
    string SchemaJson,
    string? Version,
    string? VersionDescription,
    string? SamplePayloadJson);

public sealed record UpdateEventSchemaRequest(
    string Name,
    string? Description,
    SchemaCompatibilityMode CompatibilityMode,
    SchemaStatus? Status);

public sealed record CreateSchemaVersionRequest(
    string Version,
    string SchemaJson,
    string? Description,
    string? SamplePayloadJson,
    bool SetActive = true,
    bool ForceOverrideCompatibility = false);

public sealed record CheckCompatibilityRequest(
    string? OldSchemaJson,
    string NewSchemaJson,
    SchemaCompatibilityMode Mode);

public sealed record ValidateEventPayloadRequest(
    string? EventType,
    Guid? SchemaId,
    Guid? VersionId,
    string PayloadJson);

public sealed record DetectSchemaDriftRequest(
    Guid SchemaId,
    int SampleLimit = 50);

public sealed record EventSchemaSummaryResponse(
    Guid Id,
    string EventType,
    string Name,
    string? Description,
    SchemaCompatibilityMode CompatibilityMode,
    SchemaStatus Status,
    int TotalVersions,
    string? ActiveVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EventSchemaVersionResponse(
    Guid Id,
    Guid EventSchemaId,
    string Version,
    int VersionNumber,
    string SchemaJson,
    string? Description,
    string? SamplePayloadJson,
    bool IsActive,
    bool IsDeprecated,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record EventSchemaDetailResponse(
    Guid Id,
    string EventType,
    string Name,
    string? Description,
    SchemaCompatibilityMode CompatibilityMode,
    SchemaStatus Status,
    IReadOnlyList<EventSchemaVersionResponse> Versions,
    EventSchemaVersionResponse? ActiveVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CheckCompatibilityResponse(
    bool IsCompatible,
    SchemaCompatibilityMode Mode,
    IReadOnlyList<string> BreakingChanges,
    IReadOnlyList<string> NonBreakingChanges,
    IReadOnlyList<string> Warnings);

public sealed record ValidateEventPayloadResponse(
    bool IsValid,
    string EventType,
    string Version,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record SchemaDriftIssue(
    string Path,
    string Reason,
    string Severity,
    string? SampleValue);

public sealed record DetectSchemaDriftResponse(
    Guid SchemaId,
    string EventType,
    string ActiveVersion,
    int DeliveriesAnalyzed,
    int ConformingDeliveries,
    int NonConformingDeliveries,
    double ConformanceRatePercentage,
    IReadOnlyList<SchemaDriftIssue> DetectedDrifts);

public sealed record SchemaDocumentationResponse(
    string EventType,
    string SchemaName,
    string Version,
    string MarkdownDocs,
    string TypeScriptSnippet,
    string CSharpSnippet,
    string SamplePayloadJson);
