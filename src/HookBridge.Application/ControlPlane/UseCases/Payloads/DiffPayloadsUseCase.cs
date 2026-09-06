using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Payloads;

public sealed class DiffPayloadsUseCase
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ITenantContext _tenantContext;

    public DiffPayloadsUseCase(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Result<PayloadDiffResponse> Execute(DiffPayloadsRequest request)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PayloadDiffResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (request == null)
        {
            return Result.Failure<PayloadDiffResponse>(
                DomainError.Validation("Payload.Empty", "Request cannot be null."));
        }

        var leftRaw = (request.LeftJson ?? "{}").Trim();
        var rightRaw = (request.RightJson ?? "{}").Trim();

        var leftBytes = Encoding.UTF8.GetByteCount(leftRaw);
        var rightBytes = Encoding.UTF8.GetByteCount(rightRaw);
        var byteDelta = rightBytes - leftBytes;

        JsonDocument leftDoc;
        try
        {
            leftDoc = JsonDocument.Parse(string.IsNullOrEmpty(leftRaw) ? "{}" : leftRaw);
        }
        catch (JsonException ex)
        {
            return Result.Failure<PayloadDiffResponse>(
                DomainError.Validation("Payload.InvalidLeftJson", $"Invalid Left JSON: {ex.Message}"));
        }

        JsonDocument rightDoc;
        try
        {
            rightDoc = JsonDocument.Parse(string.IsNullOrEmpty(rightRaw) ? "{}" : rightRaw);
        }
        catch (JsonException ex)
        {
            leftDoc.Dispose();
            return Result.Failure<PayloadDiffResponse>(
                DomainError.Validation("Payload.InvalidRightJson", $"Invalid Right JSON: {ex.Message}"));
        }

        using (leftDoc)
        using (rightDoc)
        {
            var entries = new List<PayloadDiffEntry>();
            CompareElements(leftDoc.RootElement, rightDoc.RootElement, "$", entries);

            var addedCount = entries.Count(e => e.DiffType == "Added");
            var removedCount = entries.Count(e => e.DiffType == "Removed");
            var modifiedCount = entries.Count(e => e.DiffType == "Modified");
            var unchangedCount = entries.Count(e => e.DiffType == "Unchanged");
            var hasDifferences = addedCount > 0 || removedCount > 0 || modifiedCount > 0;

            var response = new PayloadDiffResponse(
                hasDifferences,
                addedCount,
                removedCount,
                modifiedCount,
                unchangedCount,
                entries,
                leftBytes,
                rightBytes,
                byteDelta
            );

            return Result.Success(response);
        }
    }

    private static void CompareElements(JsonElement left, JsonElement right, string currentPath, List<PayloadDiffEntry> entries)
    {
        if (left.ValueKind != right.ValueKind)
        {
            entries.Add(new PayloadDiffEntry(
                currentPath,
                "Modified",
                FormatValue(left),
                FormatValue(right)
            ));
            return;
        }

        if (left.ValueKind == JsonValueKind.Object)
        {
            var leftProps = left.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            var rightProps = right.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

            var allKeys = leftProps.Keys.Union(rightProps.Keys).OrderBy(k => k);

            foreach (var key in allKeys)
            {
                var nextPath = $"{currentPath}.{key}";
                var inLeft = leftProps.TryGetValue(key, out var leftVal);
                var inRight = rightProps.TryGetValue(key, out var rightVal);

                if (inLeft && !inRight)
                {
                    entries.Add(new PayloadDiffEntry(
                        nextPath,
                        "Removed",
                        FormatValue(leftVal),
                        null
                    ));
                }
                else if (!inLeft && inRight)
                {
                    entries.Add(new PayloadDiffEntry(
                        nextPath,
                        "Added",
                        null,
                        FormatValue(rightVal)
                    ));
                }
                else
                {
                    CompareElements(leftVal, rightVal, nextPath, entries);
                }
            }
        }
        else if (left.ValueKind == JsonValueKind.Array)
        {
            var leftItems = left.EnumerateArray().ToList();
            var rightItems = right.EnumerateArray().ToList();
            var maxLen = Math.Max(leftItems.Count, rightItems.Count);

            for (var i = 0; i < maxLen; i++)
            {
                var nextPath = $"{currentPath}[{i}]";
                if (i >= leftItems.Count)
                {
                    entries.Add(new PayloadDiffEntry(
                        nextPath,
                        "Added",
                        null,
                        FormatValue(rightItems[i])
                    ));
                }
                else if (i >= rightItems.Count)
                {
                    entries.Add(new PayloadDiffEntry(
                        nextPath,
                        "Removed",
                        FormatValue(leftItems[i]),
                        null
                    ));
                }
                else
                {
                    CompareElements(leftItems[i], rightItems[i], nextPath, entries);
                }
            }
        }
        else
        {
            // Primitive values
            var leftStr = FormatValue(left);
            var rightStr = FormatValue(right);

            if (leftStr != rightStr)
            {
                entries.Add(new PayloadDiffEntry(
                    currentPath,
                    "Modified",
                    leftStr,
                    rightStr
                ));
            }
            else
            {
                entries.Add(new PayloadDiffEntry(
                    currentPath,
                    "Unchanged",
                    leftStr,
                    rightStr
                ));
            }
        }
    }

    private static string FormatValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Null => "null",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            _ => JsonSerializer.Serialize(element, IndentedOptions)
        };
    }
}
