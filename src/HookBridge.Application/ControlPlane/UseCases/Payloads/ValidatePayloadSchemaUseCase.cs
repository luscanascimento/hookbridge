using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Payloads;

public sealed partial class ValidatePayloadSchemaUseCase
{
    private readonly ITenantContext _tenantContext;

    public ValidatePayloadSchemaUseCase(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Result<PayloadSchemaValidationResponse> Execute(ValidatePayloadSchemaRequest request)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<PayloadSchemaValidationResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (request == null || string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Result.Failure<PayloadSchemaValidationResponse>(
                DomainError.Validation("Payload.Empty", "Payload JSON cannot be empty or null."));
        }

        if (string.IsNullOrWhiteSpace(request.SchemaJson))
        {
            return Result.Failure<PayloadSchemaValidationResponse>(
                DomainError.Validation("Schema.Empty", "Schema JSON cannot be empty or null."));
        }

        JsonDocument payloadDoc;
        try
        {
            payloadDoc = JsonDocument.Parse(request.PayloadJson.Trim());
        }
        catch (JsonException ex)
        {
            return Result.Failure<PayloadSchemaValidationResponse>(
                DomainError.Validation("Payload.InvalidJson", $"Invalid payload JSON: {ex.Message}"));
        }

        JsonDocument schemaDoc;
        try
        {
            schemaDoc = JsonDocument.Parse(request.SchemaJson.Trim());
        }
        catch (JsonException ex)
        {
            payloadDoc.Dispose();
            return Result.Failure<PayloadSchemaValidationResponse>(
                DomainError.Validation("Schema.InvalidJson", $"Invalid schema JSON: {ex.Message}"));
        }

        using (payloadDoc)
        using (schemaDoc)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateNode(payloadDoc.RootElement, schemaDoc.RootElement, "$", errors, warnings);

            var isValid = errors.Count == 0;
            var response = new PayloadSchemaValidationResponse(isValid, errors, warnings);
            return Result.Success(response);
        }
    }

    private static void ValidateNode(JsonElement payload, JsonElement schema, string path, List<string> errors, List<string> warnings)
    {
        // 1. Check type if specified in schema
        if (schema.TryGetProperty("type", out var typeProp))
        {
            var expectedType = typeProp.GetString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(expectedType))
            {
                var isTypeMatch = expectedType switch
                {
                    "object" => payload.ValueKind == JsonValueKind.Object,
                    "array" => payload.ValueKind == JsonValueKind.Array,
                    "string" => payload.ValueKind == JsonValueKind.String,
                    "number" => payload.ValueKind == JsonValueKind.Number,
                    "integer" => payload.ValueKind == JsonValueKind.Number && payload.TryGetInt64(out _),
                    "boolean" => payload.ValueKind is JsonValueKind.True or JsonValueKind.False,
                    "null" => payload.ValueKind == JsonValueKind.Null,
                    _ => true
                };

                if (!isTypeMatch)
                {
                    errors.Add($"At '{path}': Expected type '{expectedType}', but received '{payload.ValueKind.ToString().ToLowerInvariant()}'.");
                    return;
                }
            }
        }

        // 2. Format validation for strings
        if (payload.ValueKind == JsonValueKind.String && schema.TryGetProperty("format", out var formatProp))
        {
            var format = formatProp.GetString()?.ToLowerInvariant();
            var val = payload.GetString() ?? "";

            switch (format)
            {
                case "uuid":
                    if (!Guid.TryParse(val, out _))
                    {
                        errors.Add($"At '{path}': String '{val}' is not a valid UUID format.");
                    }
                    break;

                case "date-time":
                    if (!DateTimeOffset.TryParse(val, out _))
                    {
                        errors.Add($"At '{path}': String '{val}' is not a valid ISO 8601 date-time format.");
                    }
                    break;

                case "uri":
                    if (!Uri.TryCreate(val, UriKind.Absolute, out _))
                    {
                        errors.Add($"At '{path}': String '{val}' is not a valid absolute URI format.");
                    }
                    break;

                case "email":
                    if (!EmailRegex().IsMatch(val))
                    {
                        errors.Add($"At '{path}': String '{val}' is not a valid email address format.");
                    }
                    break;
            }
        }

        // 3. Object properties and required fields
        if (payload.ValueKind == JsonValueKind.Object)
        {
            // Check required fields
            if (schema.TryGetProperty("required", out var requiredProp) && requiredProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var req in requiredProp.EnumerateArray())
                {
                    var reqName = req.GetString();
                    if (!string.IsNullOrEmpty(reqName) && !payload.TryGetProperty(reqName, out _))
                    {
                        errors.Add($"At '{path}': Missing required property '{reqName}'.");
                    }
                }
            }

            // Check schema properties
            if (schema.TryGetProperty("properties", out var propsSchema) && propsSchema.ValueKind == JsonValueKind.Object)
            {
                var knownProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var propDef in propsSchema.EnumerateObject())
                {
                    knownProps.Add(propDef.Name);
                    if (payload.TryGetProperty(propDef.Name, out var childVal))
                    {
                        ValidateNode(childVal, propDef.Value, $"{path}.{propDef.Name}", errors, warnings);
                    }
                }

                // Check additional properties
                var allowAdditional = true;
                if (schema.TryGetProperty("additionalProperties", out var addlProp))
                {
                    allowAdditional = addlProp.ValueKind != JsonValueKind.False;
                }

                foreach (var payloadProp in payload.EnumerateObject())
                {
                    if (!knownProps.Contains(payloadProp.Name))
                    {
                        if (!allowAdditional)
                        {
                            errors.Add($"At '{path}': Additional property '{payloadProp.Name}' is not allowed by schema.");
                        }
                        else
                        {
                            warnings.Add($"At '{path}': Property '{payloadProp.Name}' is not explicitly defined in schema.");
                        }
                    }
                }
            }
        }

        // 4. Array validation
        if (payload.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("items", out var itemSchema) && itemSchema.ValueKind == JsonValueKind.Object)
            {
                var idx = 0;
                foreach (var item in payload.EnumerateArray())
                {
                    ValidateNode(item, itemSchema, $"{path}[{idx}]", errors, warnings);
                    idx++;
                }
            }
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
