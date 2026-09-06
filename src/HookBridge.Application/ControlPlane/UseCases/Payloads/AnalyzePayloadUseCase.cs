using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Payloads;

public sealed class AnalyzePayloadUseCase
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ITenantContext _tenantContext;

    public AnalyzePayloadUseCase(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Result<PayloadAnalysisResponse> Execute(AnalyzePayloadRequest request)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PayloadAnalysisResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (request == null || string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Result.Failure<PayloadAnalysisResponse>(
                DomainError.Validation("Payload.Empty", "Payload JSON cannot be empty or null."));
        }

        var raw = request.PayloadJson.Trim();
        var rawBytes = Encoding.UTF8.GetBytes(raw);
        var rawByteSize = rawBytes.Length;

        // Parse JSON
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            return Result.Failure<PayloadAnalysisResponse>(
                DomainError.Validation("Payload.InvalidJson", $"Invalid JSON format: {ex.Message}"));
        }

        using (doc)
        {
            var root = doc.RootElement;
            var minifiedJson = JsonSerializer.Serialize(root);
            var minifiedByteSize = Encoding.UTF8.GetByteCount(minifiedJson);

            var formattedJson = JsonSerializer.Serialize(root, IndentedOptions);
            var formattedByteSize = Encoding.UTF8.GetByteCount(formattedJson);

            // GZip Compression estimation
            var estimatedGzipByteSize = ComputeGzipByteSize(rawBytes);
            var compressionRatioPercent = rawByteSize > 0
                ? Math.Round(Math.Max(0, (1.0 - ((double)estimatedGzipByteSize / rawByteSize)) * 100.0), 2)
                : 0.0;

            // Character analysis
            var nonAsciiCount = 0;
            foreach (var b in rawBytes)
            {
                if (b > 127) nonAsciiCount++;
            }
            var isMultibyte = nonAsciiCount > 0;

            // Structural Traversal
            var stats = new StructureStats();
            TraverseElement(root, 1, stats);

            // Infer Schema
            var inferredSchemaJson = InferSchemaJson(root);

            var response = new PayloadAnalysisResponse(
                rawByteSize,
                formattedByteSize,
                minifiedByteSize,
                estimatedGzipByteSize,
                compressionRatioPercent,
                stats.TotalKeys,
                stats.MaxDepth,
                stats.ArrayCount,
                stats.ObjectCount,
                stats.StringCount,
                stats.NumberCount,
                stats.BooleanCount,
                stats.NullCount,
                nonAsciiCount,
                isMultibyte,
                "UTF-8",
                inferredSchemaJson
            );

            return Result.Success(response);
        }
    }

    private static int ComputeGzipByteSize(byte[] data)
    {
        if (data.Length == 0) return 0;
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }
        return (int)ms.Length;
    }

    private static void TraverseElement(JsonElement element, int currentDepth, StructureStats stats)
    {
        if (currentDepth > stats.MaxDepth)
        {
            stats.MaxDepth = currentDepth;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                stats.ObjectCount++;
                foreach (var prop in element.EnumerateObject())
                {
                    stats.TotalKeys++;
                    TraverseElement(prop.Value, currentDepth + 1, stats);
                }
                break;

            case JsonValueKind.Array:
                stats.ArrayCount++;
                foreach (var item in element.EnumerateArray())
                {
                    TraverseElement(item, currentDepth + 1, stats);
                }
                break;

            case JsonValueKind.String:
                stats.StringCount++;
                break;

            case JsonValueKind.Number:
                stats.NumberCount++;
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                stats.BooleanCount++;
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                stats.NullCount++;
                break;
        }
    }

    private static string InferSchemaJson(JsonElement root)
    {
        var schemaObj = InferNode(root);
        schemaObj.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        schemaObj.Insert(1, "title", "InferredWebhookPayloadSchema");
        return schemaObj.ToJsonString(IndentedOptions);
    }

    private static JsonObject InferNode(JsonElement el)
    {
        var node = new JsonObject();
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                node["type"] = "object";
                var properties = new JsonObject();
                var requiredList = new JsonArray();
                foreach (var prop in el.EnumerateObject())
                {
                    properties[prop.Name] = InferNode(prop.Value);
                    requiredList.Add(prop.Name);
                }
                node["properties"] = properties;
                if (requiredList.Count > 0)
                {
                    node["required"] = requiredList;
                }
                node["additionalProperties"] = true;
                break;

            case JsonValueKind.Array:
                node["type"] = "array";
                var items = el.EnumerateArray().ToList();
                if (items.Count > 0)
                {
                    node["items"] = InferNode(items[0]);
                }
                else
                {
                    node["items"] = new JsonObject { ["type"] = "object" };
                }
                break;

            case JsonValueKind.String:
                node["type"] = "string";
                var str = el.GetString() ?? "";
                if (Guid.TryParse(str, out _))
                {
                    node["format"] = "uuid";
                }
                else if (DateTimeOffset.TryParse(str, out _))
                {
                    node["format"] = "date-time";
                }
                else if (Uri.TryCreate(str, UriKind.Absolute, out _))
                {
                    node["format"] = "uri";
                }
                break;

            case JsonValueKind.Number:
                if (el.TryGetInt64(out _))
                {
                    node["type"] = "integer";
                }
                else
                {
                    node["type"] = "number";
                }
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                node["type"] = "boolean";
                break;

            case JsonValueKind.Null:
            default:
                node["type"] = "null";
                break;
        }

        return node;
    }

    private sealed class StructureStats
    {
        public int TotalKeys { get; set; }
        public int MaxDepth { get; set; }
        public int ArrayCount { get; set; }
        public int ObjectCount { get; set; }
        public int StringCount { get; set; }
        public int NumberCount { get; set; }
        public int BooleanCount { get; set; }
        public int NullCount { get; set; }
    }
}
