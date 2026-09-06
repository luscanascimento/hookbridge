using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed partial class ValidateEventPayloadUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ValidateEventPayloadUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ValidateEventPayloadResponse>> ExecuteAsync(
        ValidateEventPayloadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<ValidateEventPayloadResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Result.Failure<ValidateEventPayloadResponse>(
                DomainError.Validation("Payload.Empty", "Payload JSON cannot be empty."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        EventSchema? schema = null;
        EventSchemaVersion? targetVersion = null;

        if (request.VersionId.HasValue)
        {
            targetVersion = await _dbContext.EventSchemaVersions
                .Include(v => v.EventSchema)
                .FirstOrDefaultAsync(v => v.Id == request.VersionId.Value && v.TenantId == tenantId, cancellationToken);

            schema = targetVersion?.EventSchema;
        }
        else if (request.SchemaId.HasValue)
        {
            schema = await _dbContext.EventSchemas
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == request.SchemaId.Value && s.TenantId == tenantId, cancellationToken);

            targetVersion = schema?.Versions.FirstOrDefault(v => v.IsActive) ?? schema?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        }
        else if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            var normalizedEventType = request.EventType.Trim().ToLowerInvariant();
            schema = await _dbContext.EventSchemas
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.EventType == normalizedEventType && s.TenantId == tenantId, cancellationToken);

            targetVersion = schema?.Versions.FirstOrDefault(v => v.IsActive) ?? schema?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        }

        if (schema == null || targetVersion == null)
        {
            return Result.Failure<ValidateEventPayloadResponse>(
                DomainError.NotFound("EventSchema.NotFound", "No matching event schema version was found."));
        }

        JsonDocument payloadDoc;
        try
        {
            payloadDoc = JsonDocument.Parse(request.PayloadJson.Trim());
        }
        catch (JsonException ex)
        {
            return Result.Failure<ValidateEventPayloadResponse>(
                DomainError.Validation("Payload.InvalidJson", $"Payload is not valid JSON: {ex.Message}"));
        }

        JsonDocument schemaDoc;
        try
        {
            schemaDoc = JsonDocument.Parse(targetVersion.SchemaJson.Trim());
        }
        catch (JsonException ex)
        {
            payloadDoc.Dispose();
            return Result.Failure<ValidateEventPayloadResponse>(
                DomainError.Validation("Schema.InvalidJson", $"Schema is not valid JSON: {ex.Message}"));
        }

        using (payloadDoc)
        using (schemaDoc)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateJsonNode(payloadDoc.RootElement, schemaDoc.RootElement, "$", errors, warnings);

            var isValid = errors.Count == 0;
            var response = new ValidateEventPayloadResponse(
                isValid,
                schema.EventType,
                targetVersion.Version,
                errors,
                warnings);

            return Result.Success(response);
        }
    }

    private static void ValidateJsonNode(
        JsonElement payload,
        JsonElement schema,
        string path,
        List<string> errors,
        List<string> warnings)
    {
        // 1. Type validation
        if (schema.TryGetProperty("type", out var typeProp))
        {
            var expectedType = typeProp.GetString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(expectedType))
            {
                var match = expectedType switch
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

                if (!match)
                {
                    errors.Add($"At '{path}': Expected type '{expectedType}', but received '{payload.ValueKind.ToString().ToLowerInvariant()}'.");
                    return;
                }
            }
        }

        // 2. Format validation
        if (payload.ValueKind == JsonValueKind.String && schema.TryGetProperty("format", out var formatProp))
        {
            var format = formatProp.GetString()?.ToLowerInvariant();
            var val = payload.GetString() ?? "";

            switch (format)
            {
                case "uuid":
                    if (!Guid.TryParse(val, out _))
                        errors.Add($"At '{path}': '{val}' is not a valid UUID.");
                    break;
                case "date-time":
                    if (!DateTimeOffset.TryParse(val, out _))
                        errors.Add($"At '{path}': '{val}' is not a valid ISO 8601 date-time.");
                    break;
                case "email":
                    if (!EmailRegex().IsMatch(val))
                        errors.Add($"At '{path}': '{val}' is not a valid email address.");
                    break;
                case "uri":
                    if (!Uri.TryCreate(val, UriKind.Absolute, out _))
                        errors.Add($"At '{path}': '{val}' is not a valid absolute URI.");
                    break;
            }
        }

        // 3. Object validation
        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var req in reqProp.EnumerateArray())
                {
                    var reqName = req.GetString();
                    if (!string.IsNullOrEmpty(reqName) && !payload.TryGetProperty(reqName, out _))
                    {
                        errors.Add($"At '{path}': Missing required property '{reqName}'.");
                    }
                }
            }

            if (schema.TryGetProperty("properties", out var propsSchema) && propsSchema.ValueKind == JsonValueKind.Object)
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in propsSchema.EnumerateObject())
                {
                    known.Add(p.Name);
                    if (payload.TryGetProperty(p.Name, out var childVal))
                    {
                        ValidateJsonNode(childVal, p.Value, $"{path}.{p.Name}", errors, warnings);
                    }
                }

                var allowAdditional = true;
                if (schema.TryGetProperty("additionalProperties", out var addl))
                {
                    allowAdditional = addl.ValueKind != JsonValueKind.False;
                }

                foreach (var p in payload.EnumerateObject())
                {
                    if (!known.Contains(p.Name))
                    {
                        if (!allowAdditional)
                        {
                            errors.Add($"At '{path}': Additional property '{p.Name}' is not allowed.");
                        }
                        else
                        {
                            warnings.Add($"At '{path}': Undeclared property '{p.Name}' detected.");
                        }
                    }
                }
            }
        }

        // 4. Array validation
        if (payload.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out var itemSchema) &&
            itemSchema.ValueKind == JsonValueKind.Object)
        {
            var idx = 0;
            foreach (var item in payload.EnumerateArray())
            {
                ValidateJsonNode(item, itemSchema, $"{path}[{idx}]", errors, warnings);
                idx++;
            }
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
