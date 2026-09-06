using System.Text.Json;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class DetectSchemaDriftUseCase
{
    private readonly IHookBridgeDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public DetectSchemaDriftUseCase(
        IHookBridgeDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DetectSchemaDriftResponse>> ExecuteAsync(
        DetectSchemaDriftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<DetectSchemaDriftResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        var tenantId = _tenantContext.TenantId.Value;
        var schema = await _dbContext.EventSchemas
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == request.SchemaId && s.TenantId == tenantId, cancellationToken);

        if (schema == null)
        {
            return Result.Failure<DetectSchemaDriftResponse>(
                DomainError.NotFound("EventSchema.NotFound", $"Event schema with ID '{request.SchemaId}' was not found."));
        }

        var activeVersion = schema.Versions.FirstOrDefault(v => v.IsActive) ??
                            schema.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (activeVersion == null)
        {
            return Result.Failure<DetectSchemaDriftResponse>(
                DomainError.NotFound("EventSchema.NoActiveVersion", "Schema has no active version to evaluate drift against."));
        }

        JsonDocument schemaDoc;
        try
        {
            schemaDoc = JsonDocument.Parse(activeVersion.SchemaJson);
        }
        catch (JsonException ex)
        {
            return Result.Failure<DetectSchemaDriftResponse>(
                DomainError.Validation("Schema.InvalidJson", $"Active schema JSON is invalid: {ex.Message}"));
        }

        var sampleLimit = Math.Clamp(request.SampleLimit, 1, 100);

        // Fetch recent deliveries and their latest attempt request bodies
        var recentDeliveries = await _dbContext.Deliveries
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.EventType == schema.EventType)
            .OrderByDescending(d => d.CreatedAt)
            .Take(sampleLimit)
            .Include(d => d.Attempts)
            .ToListAsync(cancellationToken);

        using (schemaDoc)
        {
            var totalAnalyzed = recentDeliveries.Count;
            var conformingCount = 0;
            var nonConformingCount = 0;
            var driftMap = new Dictionary<string, SchemaDriftIssue>(StringComparer.OrdinalIgnoreCase);

            foreach (var delivery in recentDeliveries)
            {
                var latestAttempt = delivery.Attempts.OrderByDescending(a => a.AttemptNumber).FirstOrDefault();
                var payload = latestAttempt?.RequestBody;

                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                try
                {
                    using var payloadDoc = JsonDocument.Parse(payload);
                    var errors = new List<string>();
                    var warnings = new List<string>();

                    InspectDriftNode(payloadDoc.RootElement, schemaDoc.RootElement, "$", errors, warnings, driftMap);

                    if (errors.Count == 0)
                    {
                        conformingCount++;
                    }
                    else
                    {
                        nonConformingCount++;
                    }
                }
                catch
                {
                    nonConformingCount++;
                    var key = "$.root.unparseable";
                    if (!driftMap.ContainsKey(key))
                    {
                        driftMap[key] = new SchemaDriftIssue(
                            Path: "$",
                            Reason: "Payload contains malformed JSON",
                            Severity: "Error",
                            SampleValue: payload.Length > 50 ? payload[..50] + "..." : payload);
                    }
                }
            }

            var conformanceRate = totalAnalyzed > 0
                ? Math.Round((double)conformingCount / totalAnalyzed * 100.0, 2)
                : 100.0;

            var response = new DetectSchemaDriftResponse(
                SchemaId: schema.Id,
                EventType: schema.EventType,
                ActiveVersion: activeVersion.Version,
                DeliveriesAnalyzed: totalAnalyzed,
                ConformingDeliveries: conformingCount,
                NonConformingDeliveries: nonConformingCount,
                ConformanceRatePercentage: conformanceRate,
                DetectedDrifts: driftMap.Values.OrderBy(d => d.Path).ToList());

            return Result.Success(response);
        }
    }

    private static void InspectDriftNode(
        JsonElement payload,
        JsonElement schema,
        string path,
        List<string> errors,
        List<string> warnings,
        Dictionary<string, SchemaDriftIssue> driftMap)
    {
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
                    var msg = $"Expected type '{expectedType}', but received '{payload.ValueKind.ToString().ToLowerInvariant()}'.";
                    errors.Add(msg);
                    var key = $"{path}.type_mismatch";
                    if (!driftMap.ContainsKey(key))
                    {
                        driftMap[key] = new SchemaDriftIssue(path, msg, "Error", payload.ToString());
                    }
                    return;
                }
            }
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            // Check required fields
            if (schema.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var req in reqProp.EnumerateArray())
                {
                    var reqName = req.GetString();
                    if (!string.IsNullOrEmpty(reqName) && !payload.TryGetProperty(reqName, out _))
                    {
                        var msg = $"Missing required property '{reqName}'.";
                        errors.Add(msg);
                        var key = $"{path}.{reqName}.missing_required";
                        if (!driftMap.ContainsKey(key))
                        {
                            driftMap[key] = new SchemaDriftIssue($"{path}.{reqName}", msg, "Error", null);
                        }
                    }
                }
            }

            // Check schema properties
            if (schema.TryGetProperty("properties", out var propsSchema) && propsSchema.ValueKind == JsonValueKind.Object)
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in propsSchema.EnumerateObject())
                {
                    known.Add(p.Name);
                    if (payload.TryGetProperty(p.Name, out var childVal))
                    {
                        InspectDriftNode(childVal, p.Value, $"{path}.{p.Name}", errors, warnings, driftMap);
                    }
                }

                foreach (var p in payload.EnumerateObject())
                {
                    if (!known.Contains(p.Name))
                    {
                        var msg = $"Undeclared field '{p.Name}' present in payload.";
                        warnings.Add(msg);
                        var key = $"{path}.{p.Name}.undeclared";
                        if (!driftMap.ContainsKey(key))
                        {
                            driftMap[key] = new SchemaDriftIssue($"{path}.{p.Name}", msg, "Warning", p.Value.ToString());
                        }
                    }
                }
            }
        }
        else if (payload.ValueKind == JsonValueKind.Array &&
                 schema.TryGetProperty("items", out var itemSchema) &&
                 itemSchema.ValueKind == JsonValueKind.Object)
        {
            var idx = 0;
            foreach (var item in payload.EnumerateArray())
            {
                InspectDriftNode(item, itemSchema, $"{path}[{idx}]", errors, warnings, driftMap);
                idx++;
            }
        }
    }
}
