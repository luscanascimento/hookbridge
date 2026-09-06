using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.Services;
using HookBridge.Domain.Common;

namespace HookBridge.Application.ControlPlane.UseCases.Schemas;

public sealed class CheckSchemaCompatibilityUseCase
{
    private readonly ITenantContext _tenantContext;
    private readonly ISchemaCompatibilityChecker _compatibilityChecker;

    public CheckSchemaCompatibilityUseCase(
        ITenantContext tenantContext,
        ISchemaCompatibilityChecker compatibilityChecker)
    {
        _tenantContext = tenantContext;
        _compatibilityChecker = compatibilityChecker;
    }

    public Result<CheckCompatibilityResponse> Execute(CheckCompatibilityRequest request)
    {
        if (!_tenantContext.TenantId.HasValue || _tenantContext.TenantId.Value == Guid.Empty)
        {
            return Result.Failure<CheckCompatibilityResponse>(
                DomainError.Unauthorized("Tenant.Unresolved", "Tenant context could not be resolved."));
        }

        if (string.IsNullOrWhiteSpace(request.NewSchemaJson))
        {
            return Result.Failure<CheckCompatibilityResponse>(
                DomainError.Validation("Schema.EmptyNewSchema", "New schema JSON cannot be empty."));
        }

        var result = _compatibilityChecker.CheckCompatibility(
            request.OldSchemaJson ?? string.Empty,
            request.NewSchemaJson,
            request.Mode);

        var response = new CheckCompatibilityResponse(
            result.IsCompatible,
            result.Mode,
            result.BreakingChanges,
            result.NonBreakingChanges,
            result.Warnings);

        return Result.Success(response);
    }
}
