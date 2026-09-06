using System.Text.Json.Serialization;

namespace HookBridge.Application.ControlPlane.DTOs;

public sealed record AnalyzePayloadRequest(
    [property: JsonPropertyName("payloadJson")] string PayloadJson
);

public sealed record PayloadAnalysisResponse(
    [property: JsonPropertyName("rawByteSize")] int RawByteSize,
    [property: JsonPropertyName("formattedByteSize")] int FormattedByteSize,
    [property: JsonPropertyName("minifiedByteSize")] int MinifiedByteSize,
    [property: JsonPropertyName("estimatedGzipByteSize")] int EstimatedGzipByteSize,
    [property: JsonPropertyName("compressionRatioPercent")] double CompressionRatioPercent,
    [property: JsonPropertyName("totalKeys")] int TotalKeys,
    [property: JsonPropertyName("maxDepth")] int MaxDepth,
    [property: JsonPropertyName("arrayCount")] int ArrayCount,
    [property: JsonPropertyName("objectCount")] int ObjectCount,
    [property: JsonPropertyName("stringCount")] int StringCount,
    [property: JsonPropertyName("numberCount")] int NumberCount,
    [property: JsonPropertyName("booleanCount")] int BooleanCount,
    [property: JsonPropertyName("nullCount")] int NullCount,
    [property: JsonPropertyName("nonAsciiCharacterCount")] int NonAsciiCharacterCount,
    [property: JsonPropertyName("isMultibyte")] bool IsMultibyte,
    [property: JsonPropertyName("characterEncoding")] string CharacterEncoding,
    [property: JsonPropertyName("inferredSchemaJson")] string InferredSchemaJson
);

public sealed record EvaluateJsonPathRequest(
    [property: JsonPropertyName("payloadJson")] string PayloadJson,
    [property: JsonPropertyName("jsonPath")] string JsonPath
);

public sealed record JsonPathEvaluationResponse(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("matchCount")] int MatchCount,
    [property: JsonPropertyName("matches")] IReadOnlyList<JsonPathMatchItem> Matches
);

public sealed record JsonPathMatchItem(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("valueJson")] string ValueJson,
    [property: JsonPropertyName("valueType")] string ValueType
);

public sealed record DiffPayloadsRequest(
    [property: JsonPropertyName("leftJson")] string LeftJson,
    [property: JsonPropertyName("rightJson")] string RightJson
);

public sealed record PayloadDiffResponse(
    [property: JsonPropertyName("hasDifferences")] bool HasDifferences,
    [property: JsonPropertyName("addedCount")] int AddedCount,
    [property: JsonPropertyName("removedCount")] int RemovedCount,
    [property: JsonPropertyName("modifiedCount")] int ModifiedCount,
    [property: JsonPropertyName("unchangedCount")] int UnchangedCount,
    [property: JsonPropertyName("entries")] IReadOnlyList<PayloadDiffEntry> Entries,
    [property: JsonPropertyName("leftByteSize")] int LeftByteSize,
    [property: JsonPropertyName("rightByteSize")] int RightByteSize,
    [property: JsonPropertyName("byteSizeDelta")] int ByteSizeDelta
);

public sealed record PayloadDiffEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("diffType")] string DiffType, // "Added", "Removed", "Modified", "Unchanged"
    [property: JsonPropertyName("leftValue")] string? LeftValue,
    [property: JsonPropertyName("rightValue")] string? RightValue
);

public sealed record ValidatePayloadSchemaRequest(
    [property: JsonPropertyName("payloadJson")] string PayloadJson,
    [property: JsonPropertyName("schemaJson")] string SchemaJson
);

public sealed record PayloadSchemaValidationResponse(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("validationErrors")] IReadOnlyList<string> ValidationErrors,
    [property: JsonPropertyName("structuralWarnings")] IReadOnlyList<string> StructuralWarnings
);
