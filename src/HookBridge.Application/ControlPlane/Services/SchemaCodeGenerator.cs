using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HookBridge.Application.ControlPlane.Services;

public sealed class SchemaCodeGenerator : ISchemaCodeGenerator
{
    private static readonly char[] SplitChars = ['.', '-', '_', ' '];

    public SchemaDocumentationResult GenerateAll(string eventType, string schemaName, string version, string schemaJson, string? description)
    {
        var ts = GenerateTypeScript(eventType, schemaJson);
        var cs = GenerateCSharp(eventType, schemaJson);
        var md = GenerateMarkdownDocs(eventType, schemaName, version, schemaJson, description);
        var sample = GenerateSamplePayload(schemaJson);

        return new SchemaDocumentationResult(md, ts, cs, sample);
    }

    public string GenerateTypeScript(string eventType, string schemaJson)
    {
        var typeName = ToPascalCase(eventType) + "Payload";
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return string.Create(CultureInfo.InvariantCulture, $"export interface {typeName} {{\n  [key: string]: unknown;\n}}\n");
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"/**");
            sb.AppendLine(CultureInfo.InvariantCulture, $" * Payload contract for `{eventType}` event.");
            sb.AppendLine(CultureInfo.InvariantCulture, $" */");
            sb.AppendLine(CultureInfo.InvariantCulture, $"export interface {typeName} {{");

            RenderTypeScriptProperties(doc.RootElement, sb, 2);

            sb.AppendLine("}");
            return sb.ToString();
        }
        catch
        {
            return string.Create(CultureInfo.InvariantCulture, $"export interface {typeName} {{\n  [key: string]: unknown;\n}}\n");
        }
    }

    private static void RenderTypeScriptProperties(JsonElement node, StringBuilder sb, int indent)
    {
        var indentStr = new string(' ', indent);
        var requiredSet = GetRequiredProperties(node);

        if (node.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                var isRequired = requiredSet.Contains(prop.Name);
                var tsType = ResolveTypeScriptType(prop.Value);
                var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() : null;

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{indentStr}/** {desc} */");
                }

                var optFlag = isRequired ? "" : "?";
                sb.AppendLine(CultureInfo.InvariantCulture, $"{indentStr}{prop.Name}{optFlag}: {tsType};");
            }
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indentStr}[key: string]: unknown;");
        }
    }

    private static string ResolveTypeScriptType(JsonElement element)
    {
        if (element.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            var values = enumProp.EnumerateArray().Select(v => string.Create(CultureInfo.InvariantCulture, $"'{v.GetString()}'"));
            return string.Join(" | ", values);
        }

        if (element.TryGetProperty("type", out var typeProp))
        {
            var t = typeProp.GetString()?.ToLowerInvariant();
            return t switch
            {
                "string" => ResolveTypeScriptStringType(element),
                "number" or "integer" => "number",
                "boolean" => "boolean",
                "array" => ResolveTypeScriptArrayType(element),
                "object" => ResolveTypeScriptObject(element),
                "null" => "null",
                _ => "unknown"
            };
        }

        return "unknown";
    }

    private static string ResolveTypeScriptStringType(JsonElement element)
    {
        if (element.TryGetProperty("format", out var formatProp))
        {
            var fmt = formatProp.GetString()?.ToLowerInvariant();
            return fmt switch
            {
                "date-time" => "string /* ISO 8601 */",
                "uuid" => "string /* UUID */",
                "email" => "string /* Email */",
                "uri" => "string /* URI */",
                _ => "string"
            };
        }
        return "string";
    }

    private static string ResolveTypeScriptArrayType(JsonElement element)
    {
        if (element.TryGetProperty("items", out var itemProp))
        {
            var innerType = ResolveTypeScriptType(itemProp);
            return string.Create(CultureInfo.InvariantCulture, $"{innerType}[]");
        }
        return "unknown[]";
    }

    private static string ResolveTypeScriptObject(JsonElement element)
    {
        if (element.TryGetProperty("properties", out var subProps) && subProps.ValueKind == JsonValueKind.Object)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            RenderTypeScriptProperties(element, sb, 4);
            sb.Append("  }");
            return sb.ToString();
        }
        return "Record<string, unknown>";
    }

    public string GenerateCSharp(string eventType, string schemaJson)
    {
        var typeName = ToPascalCase(eventType) + "Payload";
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return string.Create(CultureInfo.InvariantCulture, $"public sealed record {typeName}(\n    Dictionary<string, object?> AdditionalData);\n");
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var sb = new StringBuilder();
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"/// <summary>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"/// Webhook payload representation for <c>{eventType}</c>.");
            sb.AppendLine(CultureInfo.InvariantCulture, $"/// </summary>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed record {typeName}(");

            var requiredSet = GetRequiredProperties(doc.RootElement);
            if (doc.RootElement.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                var propList = props.EnumerateObject().ToList();
                for (var i = 0; i < propList.Count; i++)
                {
                    var prop = propList[i];
                    var isRequired = requiredSet.Contains(prop.Name);
                    var csType = ResolveCSharpType(prop.Value, isRequired);
                    var csName = ToPascalCase(prop.Name);
                    var comma = i < propList.Count - 1 ? "," : ");";

                    sb.AppendLine(CultureInfo.InvariantCulture, $"    [property: JsonPropertyName(\"{prop.Name}\")] {csType} {csName}{comma}");
                }
            }
            else
            {
                sb.AppendLine("    [property: JsonPropertyName(\"data\")] Dictionary<string, object?> Data);");
            }

            return sb.ToString();
        }
        catch
        {
            return string.Create(CultureInfo.InvariantCulture, $"public sealed record {typeName}(\n    Dictionary<string, object?> Data);\n");
        }
    }

    private static string ResolveCSharpType(JsonElement element, bool isRequired)
    {
        if (element.TryGetProperty("type", out var typeProp))
        {
            var t = typeProp.GetString()?.ToLowerInvariant();
            var format = element.TryGetProperty("format", out var f) ? f.GetString()?.ToLowerInvariant() : null;

            return t switch
            {
                "string" when format == "uuid" => isRequired ? "Guid" : "Guid?",
                "string" when format == "date-time" => isRequired ? "DateTimeOffset" : "DateTimeOffset?",
                "string" => isRequired ? "string" : "string?",
                "integer" => isRequired ? "long" : "long?",
                "number" => isRequired ? "decimal" : "decimal?",
                "boolean" => isRequired ? "bool" : "bool?",
                "array" => ResolveCSharpArrayType(element, isRequired),
                "object" => isRequired ? "Dictionary<string, object?>" : "Dictionary<string, object?>?",
                _ => "object?"
            };
        }

        return "object?";
    }

    private static string ResolveCSharpArrayType(JsonElement element, bool isRequired)
    {
        if (element.TryGetProperty("items", out var itemProp))
        {
            var inner = ResolveCSharpType(itemProp, true);
            return isRequired ? string.Create(CultureInfo.InvariantCulture, $"IReadOnlyList<{inner}>") : string.Create(CultureInfo.InvariantCulture, $"IReadOnlyList<{inner}>?");
        }
        return isRequired ? "IReadOnlyList<object>" : "IReadOnlyList<object>?";
    }

    public string GenerateMarkdownDocs(string eventType, string schemaName, string version, string schemaJson, string? description)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Event Schema: `{eventType}`");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Name:** {schemaName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Version:** `{version}`");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **Description:** {description}");
        }
        sb.AppendLine();
        sb.AppendLine("## Payload Attributes");
        sb.AppendLine();
        sb.AppendLine("| Field | Type | Required | Format / Enum | Description |");
        sb.AppendLine("| :--- | :--- | :---: | :--- | :--- |");

        if (!string.IsNullOrWhiteSpace(schemaJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(schemaJson);
                var requiredSet = GetRequiredProperties(doc.RootElement);

                if (doc.RootElement.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        var isReq = requiredSet.Contains(prop.Name) ? "✅ Yes" : "No";
                        var type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "any" : "any";
                        var formatOrEnum = GetFormatOrEnumSummary(prop.Value);
                        var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "-" : "-";

                        sb.AppendLine(CultureInfo.InvariantCulture, $"| `{prop.Name}` | `{type}` | {isReq} | {formatOrEnum} | {desc} |");
                    }
                }
            }
            catch
            {
                sb.AppendLine("| `payload` | `object` | ✅ Yes | - | Payload object |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Example Payload");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(GenerateSamplePayload(schemaJson));
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string GetFormatOrEnumSummary(JsonElement element)
    {
        if (element.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            var items = enumProp.EnumerateArray().Select(v => string.Create(CultureInfo.InvariantCulture, $"`{v.GetString()}`"));
            return "Enum: " + string.Join(", ", items);
        }

        if (element.TryGetProperty("format", out var formatProp))
        {
            return string.Create(CultureInfo.InvariantCulture, $"`{formatProp.GetString()}`");
        }

        return "-";
    }

    public string GenerateSamplePayload(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return "{\n  \"id\": \"evt_sample_12345\",\n  \"status\": \"success\"\n}";
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var rootNode = GenerateSampleNode(doc.RootElement);
            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return "{\n  \"id\": \"evt_sample_12345\"\n}";
        }
    }

    private static JsonNode GenerateSampleNode(JsonElement element)
    {
        if (element.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array && enumProp.GetArrayLength() > 0)
        {
            var first = enumProp[0];
            return JsonValue.Create(first.GetString() ?? "default")!;
        }

        if (element.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString()?.ToLowerInvariant();
            var format = element.TryGetProperty("format", out var f) ? f.GetString()?.ToLowerInvariant() : null;

            switch (typeStr)
            {
                case "string":
                    return format switch
                    {
                        "uuid" => JsonValue.Create("a1b2c3d4-e5f6-7890-abcd-ef1234567890")!,
                        "date-time" => JsonValue.Create(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))!,
                        "email" => JsonValue.Create("customer@example.com")!,
                        "uri" => JsonValue.Create("https://api.example.com/v1/resource/123")!,
                        _ => JsonValue.Create(GetSampleStringForField(element))!
                    };

                case "integer":
                    return JsonValue.Create(42)!;

                case "number":
                    return JsonValue.Create(199.99)!;

                case "boolean":
                    return JsonValue.Create(true)!;

                case "array":
                    var arr = new JsonArray();
                    if (element.TryGetProperty("items", out var itemSchema))
                    {
                        arr.Add(GenerateSampleNode(itemSchema));
                    }
                    else
                    {
                        arr.Add(JsonValue.Create("sample_item")!);
                    }
                    return arr;

                case "object":
                    var obj = new JsonObject();
                    if (element.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in props.EnumerateObject())
                        {
                            obj[prop.Name] = GenerateSampleNode(prop.Value);
                        }
                    }
                    return obj;
            }
        }

        return JsonValue.Create("sample_value")!;
    }

    private static string GetSampleStringForField(JsonElement element)
    {
        if (element.TryGetProperty("title", out var title))
        {
            return string.Create(CultureInfo.InvariantCulture, $"sample_{title.GetString()?.ToLowerInvariant()}");
        }
        return "sample_value";
    }

    private static HashSet<string> GetRequiredProperties(JsonElement element)
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

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Webhook";
        var parts = text.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1)
                {
                    sb.Append(p.AsSpan(1));
                }
            }
        }
        return sb.ToString();
    }
}
