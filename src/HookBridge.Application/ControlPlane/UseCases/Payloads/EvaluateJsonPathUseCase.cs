using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Payloads;

public sealed partial class EvaluateJsonPathUseCase
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ITenantContext _tenantContext;

    public EvaluateJsonPathUseCase(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Result<JsonPathEvaluationResponse> Execute(EvaluateJsonPathRequest request)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<JsonPathEvaluationResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (request == null || string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Result.Failure<JsonPathEvaluationResponse>(
                DomainError.Validation("Payload.Empty", "Payload JSON cannot be empty or null."));
        }

        var path = string.IsNullOrWhiteSpace(request.JsonPath) ? "$" : request.JsonPath.Trim();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(request.PayloadJson.Trim());
        }
        catch (JsonException ex)
        {
            return Result.Failure<JsonPathEvaluationResponse>(
                DomainError.Validation("Payload.InvalidJson", $"Invalid JSON format: {ex.Message}"));
        }

        using (doc)
        {
            try
            {
                var matches = new List<JsonPathMatchItem>();
                EvaluatePath(doc.RootElement, "$", path, matches);

                var response = new JsonPathEvaluationResponse(
                    IsValid: true,
                    ErrorMessage: null,
                    MatchCount: matches.Count,
                    Matches: matches
                );

                return Result.Success(response);
            }
            catch (Exception ex)
            {
                var response = new JsonPathEvaluationResponse(
                    IsValid: false,
                    ErrorMessage: ex.Message,
                    MatchCount: 0,
                    Matches: Array.Empty<JsonPathMatchItem>()
                );

                return Result.Success(response);
            }
        }
    }

    private static void EvaluatePath(JsonElement root, string currentPath, string query, List<JsonPathMatchItem> matches)
    {
        var normalizedQuery = query.Trim();

        // 1. Check for recursive descent e.g. "$..prop" or "..prop"
        if (normalizedQuery.StartsWith("$..", StringComparison.Ordinal))
        {
            var targetProp = normalizedQuery.Substring(3).Trim();
            FindRecursive(root, "$", targetProp, matches);
            return;
        }

        if (normalizedQuery.StartsWith("..", StringComparison.Ordinal))
        {
            var targetProp = normalizedQuery.Substring(2).Trim();
            FindRecursive(root, "$", targetProp, matches);
            return;
        }

        // 2. Strip leading "$" or "$."
        if (normalizedQuery.StartsWith("$.", StringComparison.Ordinal))
        {
            normalizedQuery = normalizedQuery.Substring(2);
        }
        else if (normalizedQuery.StartsWith('$'))
        {
            normalizedQuery = normalizedQuery.Substring(1);
        }

        if (string.IsNullOrEmpty(normalizedQuery))
        {
            AddMatch(root, "$", matches);
            return;
        }

        // Tokenize path: splits by '.' and handles '[0]' or '[*]'
        var segments = ParseSegments(normalizedQuery);
        ResolveSegments(root, "$", segments, 0, matches);
    }

    private static List<PathSegment> ParseSegments(string path)
    {
        var segments = new List<PathSegment>();
        var tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var t = token.Trim();
            var arrayMatch = ArrayIndexerRegex().Match(t);
            if (arrayMatch.Success)
            {
                var propName = arrayMatch.Groups[1].Value;
                var indexStr = arrayMatch.Groups[2].Value;

                if (!string.IsNullOrEmpty(propName))
                {
                    segments.Add(new PathSegment(propName, false, 0, false));
                }

                if (indexStr == "*")
                {
                    segments.Add(new PathSegment("", false, 0, true));
                }
                else if (int.TryParse(indexStr, out var idx))
                {
                    segments.Add(new PathSegment("", true, idx, false));
                }
            }
            else
            {
                segments.Add(new PathSegment(t, false, 0, false));
            }
        }

        return segments;
    }

    private static void ResolveSegments(JsonElement current, string currentPath, List<PathSegment> segments, int index, List<JsonPathMatchItem> matches)
    {
        if (index >= segments.Count)
        {
            AddMatch(current, currentPath, matches);
            return;
        }

        var segment = segments[index];

        if (segment.IsArrayWildcard)
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var item in current.EnumerateArray())
                {
                    ResolveSegments(item, $"{currentPath}[{i}]", segments, index + 1, matches);
                    i++;
                }
            }
            return;
        }

        if (segment.IsArrayIndex)
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var item in current.EnumerateArray())
                {
                    if (i == segment.Index)
                    {
                        ResolveSegments(item, $"{currentPath}[{i}]", segments, index + 1, matches);
                        break;
                    }
                    i++;
                }
            }
            return;
        }

        if (segment.Name == "*")
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in current.EnumerateObject())
                {
                    ResolveSegments(prop.Value, $"{currentPath}.{prop.Name}", segments, index + 1, matches);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var item in current.EnumerateArray())
                {
                    ResolveSegments(item, $"{currentPath}[{i}]", segments, index + 1, matches);
                    i++;
                }
            }
            return;
        }

        // Property match
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in current.EnumerateObject())
            {
                if (string.Equals(prop.Name, segment.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ResolveSegments(prop.Value, $"{currentPath}.{prop.Name}", segments, index + 1, matches);
                }
            }
        }
    }

    private static void FindRecursive(JsonElement current, string currentPath, string targetProp, List<JsonPathMatchItem> matches)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in current.EnumerateObject())
            {
                var newPath = $"{currentPath}.{prop.Name}";
                if (string.Equals(prop.Name, targetProp, StringComparison.OrdinalIgnoreCase) || targetProp == "*")
                {
                    AddMatch(prop.Value, newPath, matches);
                }
                FindRecursive(prop.Value, newPath, targetProp, matches);
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in current.EnumerateArray())
            {
                var newPath = $"{currentPath}[{i}]";
                FindRecursive(item, newPath, targetProp, matches);
                i++;
            }
        }
    }

    private static void AddMatch(JsonElement element, string path, List<JsonPathMatchItem> matches)
    {
        var valueJson = JsonSerializer.Serialize(element, IndentedOptions);
        var valueType = element.ValueKind.ToString();

        matches.Add(new JsonPathMatchItem(path, valueJson, valueType));
    }

    private sealed record PathSegment(string Name, bool IsArrayIndex, int Index, bool IsArrayWildcard);

    [GeneratedRegex(@"^([^\[]*)\[(\*|\d+)\]$")]
    private static partial Regex ArrayIndexerRegex();
}
