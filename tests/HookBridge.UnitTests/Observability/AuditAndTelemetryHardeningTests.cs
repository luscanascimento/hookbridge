using System.Diagnostics;
using HookBridge.Application.Abstractions;
using HookBridge.Application.ControlPlane.DTOs;
using HookBridge.Application.ControlPlane.UseCases.Deliveries;
using HookBridge.Domain.Common;
using HookBridge.Domain.Diagnostics;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using HookBridge.Infrastructure.Persistence;
using NSubstitute;
using Xunit;

namespace HookBridge.UnitTests.Observability;

public sealed class AuditAndTelemetryHardeningTests
{
    [Fact]
    public void AuditEntry_Create_AutomaticallyScrubsSensitivePropertiesInDetails()
    {
        var tenantId = Guid.NewGuid();
        var detailsWithSecrets = """
        {
            "adminPassword": "UltraSecretPassword123!",
            "clientSecret": "sec_live_99999",
            "token": "bearer_token_xyz",
            "status": "Active",
            "environment": "Production"
        }
        """;

        var result = AuditEntry.Create(
            tenantId,
            Guid.NewGuid(),
            "User.Registered",
            "Tenant",
            tenantId.ToString(),
            detailsWithSecrets,
            "127.0.0.1",
            null,
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        var entry = result.Value;

        Assert.DoesNotContain("UltraSecretPassword123!", entry.DetailsJson);
        Assert.DoesNotContain("sec_live_99999", entry.DetailsJson);
        Assert.DoesNotContain("bearer_token_xyz", entry.DetailsJson);
        Assert.Contains("\"adminPassword\":\"[REDACTED]\"", entry.DetailsJson);
        Assert.Contains("\"clientSecret\":\"[REDACTED]\"", entry.DetailsJson);
        Assert.Contains("\"token\":\"[REDACTED]\"", entry.DetailsJson);
        Assert.Contains("\"status\":\"Active\"", entry.DetailsJson);
        Assert.Contains("\"environment\":\"Production\"", entry.DetailsJson);
    }

    [Fact]
    public void AuditEntry_Create_CapturesAmbientTraceIdWhenNullPassed()
    {
        var tenantId = Guid.NewGuid();
        using var activity = new Activity("TestAuditActivity").Start();

        var result = AuditEntry.Create(
            tenantId,
            null,
            "Endpoint.Created",
            "Endpoint",
            Guid.NewGuid().ToString(),
            "{\"status\":\"Active\"}",
            "10.0.0.1",
            null, // Null trace ID passed
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(activity.TraceId.ToString(), result.Value.TraceId);
    }

    [Fact]
    public async Task RecordDeliveryAttemptUseCase_SanitizesRequestAndResponseHeaders()
    {
        var tenantId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.HasTenant.Returns(true);

        var dbOptions = new DbContextOptionsBuilder<HookBridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new HookBridgeDbContext(dbOptions, tenantContext);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        var delivery = Delivery.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "order.created",
            "corr_123",
            "00-trace-01",
            DateTimeOffset.UtcNow).Value;

        var idProp = typeof(Delivery).GetProperty("Id");
        idProp?.SetValue(delivery, deliveryId);

        dbContext.Deliveries.Add(delivery);
        await dbContext.SaveChangesAsync();

        var useCase = new RecordDeliveryAttemptUseCase(dbContext, tenantContext, dateTimeProvider);

        var command = new RecordDeliveryAttemptCommand(
            HttpStatusCode: 200,
            RequestHeadersJson: "{\"Authorization\":\"Bearer secret_key_to_redact\",\"Content-Type\":\"application/json\"}",
            RequestBody: "{\"orderId\":\"123\"}",
            ResponseHeadersJson: "{\"Set-Cookie\":\"session_id=12345; Secure\",\"Server\":\"Kestrel\"}",
            ResponseBody: "{\"status\":\"ok\"}",
            ElapsedMs: 45,
            ErrorMessage: null,
            FinalStatus: DeliveryStatus.Success);

        var response = await useCase.ExecuteAsync(deliveryId, command);

        Assert.True(response.IsSuccess);

        var capturedAttempt = await dbContext.Attempts.FirstOrDefaultAsync(a => a.DeliveryId == deliveryId);
        Assert.NotNull(capturedAttempt);

        // Verify request headers were sanitized
        Assert.DoesNotContain("secret_key_to_redact", capturedAttempt.RequestHeadersJson);
        Assert.Contains("\"Authorization\":\"Bearer [REDACTED]\"", capturedAttempt.RequestHeadersJson);
        Assert.Contains("\"Content-Type\":\"application/json\"", capturedAttempt.RequestHeadersJson);

        // Verify response headers were sanitized
        Assert.DoesNotContain("session_id=12345", capturedAttempt.ResponseHeadersJson);
        Assert.Contains("\"Set-Cookie\":\"[REDACTED]\"", capturedAttempt.ResponseHeadersJson);
        Assert.Contains("\"Server\":\"Kestrel\"", capturedAttempt.ResponseHeadersJson);
    }
}
