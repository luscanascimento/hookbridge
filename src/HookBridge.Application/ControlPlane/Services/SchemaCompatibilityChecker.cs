using System.Text.Json;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.ControlPlane.Services;

public sealed class SchemaCompatibilityChecker : ISchemaCompatibilityChecker
{
    private static readonly string[] ModeNoneNonBreaking = ["Compatibility check bypassed (Mode is None)."];
    private static readonly string[] InitialSchemaNonBreaking = ["Initial schema version (no previous schema to compare)."];

    public SchemaCompatibilityCheckResult CheckCompatibility(
        string oldSchemaJson,
        string newSchemaJson,
        SchemaCompatibilityMode mode)
    {
        if (mode == SchemaCompatibilityMode.None)
        {
            return new SchemaCompatibilityCheckResult(
                IsCompatible: true,
                Mode: mode,
                BreakingChanges: Array.Empty<string>(),
                NonBreakingChanges: ModeNoneNonBreaking,
                Warnings: Array.Empty<string>());
        }

        if (string.IsNullOrWhiteSpace(oldSchemaJson))
        {
            return new SchemaCompatibilityCheckResult(
                IsCompatible: true,
                Mode: mode,
                BreakingChanges: Array.Empty<string>(),
                NonBreakingChanges: InitialSchemaNonBreaking,
                Warnings: Array.Empty<string>());
        }

        JsonDocument oldDoc;
        try
        {
            oldDoc = JsonDocument.Parse(oldSchemaJson.Trim());
        }
        catch (JsonException ex)
        {
            return new SchemaCompatibilityCheckResult(
                IsCompatible: false,
                Mode: mode,
                BreakingChanges: [$"Old schema is not valid JSON: {ex.Message}"],
                NonBreakingChanges: Array.Empty<string>(),
                Warnings: Array.Empty<string>());
        }

        JsonDocument newDoc;
        try
        {
            newDoc = JsonDocument.Parse(newSchemaJson.Trim());
        }
        catch (JsonException ex)
        {
            oldDoc.Dispose();
            return new SchemaCompatibilityCheckResult(
                IsCompatible: false,
                Mode: mode,
                BreakingChanges: [$"New schema is not valid JSON: {ex.Message}"],
                NonBreakingChanges: Array.Empty<string>(),
                Warnings: Array.Empty<string>());
        }

        using (oldDoc)
        using (newDoc)
        {
            var breakingChanges = new List<string>();
            var nonBreakingChanges = new List<string>();
            var warnings = new List<string>();

            CompareSchemas(
                oldDoc.RootElement,
                newDoc.RootElement,
                "$",
                mode,
                breakingChanges,
                nonBreakingChanges,
                warnings);

            var isCompatible = breakingChanges.Count == 0;

            return new SchemaCompatibilityCheckResult(
                IsCompatible: isCompatible,
                Mode: mode,
                BreakingChanges: breakingChanges,
                NonBreakingChanges: nonBreakingChanges,
                Warnings: warnings);
        }
    }

    private static void CompareSchemas(
        JsonElement oldNode,
        JsonElement newNode,
        string path,
        SchemaCompatibilityMode mode,
        List<string> breaking,
        List<string> nonBreaking,
        List<string> warnings)
    {
        // 1. Compare 'type'
        var oldType = GetStringType(oldNode);
        var newType = GetStringType(newNode);

        if (!string.IsNullOrEmpty(oldType) && !string.IsNullOrEmpty(newType) &&
            !string.Equals(oldType, newType, StringComparison.OrdinalIgnoreCase))
        {
            breaking.Add($"At '{path}': Type changed from '{oldType}' to '{newType}'.");
            return;
        }

        // 2. Object properties comparison
        var oldProps = GetProperties(oldNode);
        var newProps = GetProperties(newNode);
        var oldRequired = GetRequiredSet(oldNode);
        var newRequired = GetRequiredSet(newNode);

        if (oldProps.Count > 0 || newProps.Count > 0)
        {
            // Check removed properties
            foreach (var oldProp in oldProps)
            {
                if (!newProps.ContainsKey(oldProp.Key))
                {
                    // Property removed in new schema
                    if (oldRequired.Contains(oldProp.Key))
                    {
                        if (mode is SchemaCompatibilityMode.Forward or SchemaCompatibilityMode.Full)
                        {
                            breaking.Add($"At '{path}.{oldProp.Key}': Required property was removed (violates {mode} compatibility).");
                        }
                        else
                        {
                            nonBreaking.Add($"At '{path}.{oldProp.Key}': Required property was removed in new version.");
                        }
                    }
                    else
                    {
                        if (mode is SchemaCompatibilityMode.Forward or SchemaCompatibilityMode.Full)
                        {
                            warnings.Add($"At '{path}.{oldProp.Key}': Optional property was removed in new version.");
                        }
                        else
                        {
                            nonBreaking.Add($"At '{path}.{oldProp.Key}': Optional property was removed.");
                        }
                    }
                }
            }

            // Check added or updated properties
            foreach (var newProp in newProps)
            {
                if (!oldProps.TryGetValue(newProp.Key, out var oldPropElement))
                {
                    // Property added in new schema
                    var isNewReq = newRequired.Contains(newProp.Key);
                    if (isNewReq)
                    {
                        if (mode is SchemaCompatibilityMode.Backward or SchemaCompatibilityMode.Full)
                        {
                            breaking.Add($"At '{path}.{newProp.Key}': New required property added without default (violates {mode} compatibility).");
                        }
                        else
                        {
                            nonBreaking.Add($"At '{path}.{newProp.Key}': New required property added.");
                        }
                    }
                    else
                    {
                        nonBreaking.Add($"At '{path}.{newProp.Key}': New optional property added.");
                    }
                }
                else
                {
                    // Property exists in both -> check required status change
                    var wasRequired = oldRequired.Contains(newProp.Key);
                    var isNowRequired = newRequired.Contains(newProp.Key);

                    if (!wasRequired && isNowRequired)
                    {
                        if (mode is SchemaCompatibilityMode.Backward or SchemaCompatibilityMode.Full)
                        {
                            breaking.Add($"At '{path}.{newProp.Key}': Optional property became required (violates {mode} compatibility).");
                        }
                        else
                        {
                            nonBreaking.Add($"At '{path}.{newProp.Key}': Property promoted to required.");
                        }
                    }
                    else if (wasRequired && !isNowRequired)
                    {
                        if (mode is SchemaCompatibilityMode.Forward or SchemaCompatibilityMode.Full)
                        {
                            warnings.Add($"At '{path}.{newProp.Key}': Required property relaxed to optional.");
                        }
                        else
                        {
                            nonBreaking.Add($"At '{path}.{newProp.Key}': Required property relaxed to optional.");
                        }
                    }

                    CompareSchemas(
                        oldPropElement,
                        newProp.Value,
                        $"{path}.{newProp.Key}",
                        mode,
                        breaking,
                        nonBreaking,
                        warnings);
                }
            }
        }

        // 3. Array items comparison
        if (oldNode.TryGetProperty("items", out var oldItems) && newNode.TryGetProperty("items", out var newItems))
        {
            if (oldItems.ValueKind == JsonValueKind.Object && newItems.ValueKind == JsonValueKind.Object)
            {
                CompareSchemas(
                    oldItems,
                    newItems,
                    $"{path}[*]",
                    mode,
                    breaking,
                    nonBreaking,
                    warnings);
            }
        }

        // 4. Enums comparison
        if (oldNode.TryGetProperty("enum", out var oldEnum) && newNode.TryGetProperty("enum", out var newEnum))
        {
            if (oldEnum.ValueKind == JsonValueKind.Array && newEnum.ValueKind == JsonValueKind.Array)
            {
                var oldValues = oldEnum.EnumerateArray().Select(v => v.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var newValues = newEnum.EnumerateArray().Select(v => v.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var removed = oldValues.Except(newValues).ToList();
                if (removed.Count > 0 && mode is SchemaCompatibilityMode.Backward or SchemaCompatibilityMode.Full)
                {
                    breaking.Add($"At '{path}': Enum values removed: [{string.Join(", ", removed)}] (violates {mode} compatibility).");
                }

                var added = newValues.Except(oldValues).ToList();
                if (added.Count > 0 && mode is SchemaCompatibilityMode.Forward or SchemaCompatibilityMode.Full)
                {
                    breaking.Add($"At '{path}': Enum values added: [{string.Join(", ", added)}] (violates {mode} compatibility).");
                }
            }
        }
    }

    private static string? GetStringType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("type", out var typeProp))
        {
            return typeProp.GetString();
        }
        return null;
    }

    private static Dictionary<string, JsonElement> GetProperties(JsonElement element)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("properties", out var props) &&
            props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                dict[prop.Name] = prop.Value;
            }
        }
        return dict;
    }

    private static HashSet<string> GetRequiredSet(JsonElement element)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("required", out var req) &&
            req.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in req.EnumerateArray())
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    set.Add(str);
                }
            }
        }
        return set;
    }
}
