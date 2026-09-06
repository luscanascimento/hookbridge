using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.Services;

public sealed record SchemaCompatibilityCheckResult(
    bool IsCompatible,
    SchemaCompatibilityMode Mode,
    IReadOnlyList<string> BreakingChanges,
    IReadOnlyList<string> NonBreakingChanges,
    IReadOnlyList<string> Warnings);

public interface ISchemaCompatibilityChecker
{
    SchemaCompatibilityCheckResult CheckCompatibility(
        string oldSchemaJson,
        string newSchemaJson,
        SchemaCompatibilityMode mode);
}
