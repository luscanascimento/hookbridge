using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Application.Common;
using HookBridge.Application.ControlPlane.UseCases.Applications;
using HookBridge.Application.ControlPlane.UseCases.Endpoints;
using HookBridge.Application.ControlPlane.UseCases.Subscriptions;
using HookBridge.Application.ControlPlane.UseCases.WebhookSecrets;
using HookBridge.Application.ControlPlane.UseCases.ApiKeys;
using HookBridge.Application.ControlPlane.UseCases.AuditLogs;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Auth Use Cases
        services.AddScoped<RegisterTenantUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<InviteUserUseCase>();
        services.AddScoped<GetCurrentUserUseCase>();

        // Control Plane: Applications
        services.AddScoped<CreateApplicationUseCase>();
        services.AddScoped<GetApplicationsUseCase>();
        services.AddScoped<GetApplicationByIdUseCase>();
        services.AddScoped<UpdateApplicationUseCase>();
        services.AddScoped<DeleteApplicationUseCase>();

        // Control Plane: Endpoints
        services.AddScoped<CreateEndpointUseCase>();
        services.AddScoped<GetEndpointsUseCase>();
        services.AddScoped<GetEndpointByIdUseCase>();
        services.AddScoped<UpdateEndpointUseCase>();
        services.AddScoped<UpdateEndpointStatusUseCase>();
        services.AddScoped<DeleteEndpointUseCase>();
        services.AddScoped<GetEndpointHealthMetricsUseCase>();
        services.AddScoped<GetTenantEndpointsHealthSummaryUseCase>();

        // Control Plane: Subscriptions
        services.AddScoped<CreateSubscriptionUseCase>();
        services.AddScoped<GetSubscriptionsByEndpointUseCase>();
        services.AddScoped<DeleteSubscriptionUseCase>();

        // Control Plane: Webhook Secrets
        services.AddScoped<GetEndpointSecretsUseCase>();
        services.AddScoped<RotateWebhookSecretUseCase>();
        services.AddScoped<RevokeWebhookSecretUseCase>();

        // Control Plane: API Keys
        services.AddScoped<CreateApiKeyUseCase>();
        services.AddScoped<GetApiKeysUseCase>();
        services.AddScoped<RevokeApiKeyUseCase>();

        // Control Plane: Audit Logs
        services.AddScoped<GetAuditLogsUseCase>();

        // Control Plane: Webhook Signing
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.WebhookSigning.GenerateEndpointSignatureUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.WebhookSigning.VerifyEndpointSignatureUseCase>();

        // Control Plane: Event Publishing Pipeline
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Publishing.PublishEventUseCase>();

        // Control Plane: Dead Letter Queue Management
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.DeadLetter.PeekDeadLettersUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.DeadLetter.ReplayDeadLettersUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.DeadLetter.PurgeDeadLettersUseCase>();

        // Control Plane: Deliveries and Attempts Tracking
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.GetDeliveriesUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.GetDeliveryByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.GetDeliveryStatsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.RecordDeliveryAttemptUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.ReplayDeliveryUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.BulkReplayDeliveriesUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Deliveries.GetDeliveryLineageUseCase>();

        // Control Plane: Distributed Trace Correlation Explorer
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Traces.GetTracesUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Traces.GetTraceDetailUseCase>();

        // Control Plane: Payload Inspector & Analysis
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Payloads.AnalyzePayloadUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Payloads.EvaluateJsonPathUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Payloads.DiffPayloadsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Payloads.ValidatePayloadSchemaUseCase>();

        // Control Plane: Event Schemas & Versioning Registry
        services.AddSingleton<HookBridge.Application.ControlPlane.Services.ISchemaCompatibilityChecker, HookBridge.Application.ControlPlane.Services.SchemaCompatibilityChecker>();
        services.AddSingleton<HookBridge.Application.ControlPlane.Services.ISchemaCodeGenerator, HookBridge.Application.ControlPlane.Services.SchemaCodeGenerator>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.CreateEventSchemaUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.GetEventSchemasUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.GetEventSchemaByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.UpdateEventSchemaUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.DeleteEventSchemaUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.CreateSchemaVersionUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.ActivateSchemaVersionUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.DeprecateSchemaVersionUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.CheckSchemaCompatibilityUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.ValidateEventPayloadUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.DetectSchemaDriftUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Schemas.GenerateSchemaDocsUseCase>();

        // Control Plane: Developer Documentation & Code Snippets
        services.AddSingleton<HookBridge.Application.ControlPlane.Services.IDocSnippetGenerator, HookBridge.Application.ControlPlane.Services.DocSnippetGenerator>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Documentation.GetApiReferenceUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Documentation.GenerateDocSnippetUseCase>();

        // Control Plane: Webhook Sandbox & Realtime Inspection
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.CreateSandboxUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.GetSandboxesUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.GetSandboxByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.UpdateSandboxConfigUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.DeleteSandboxUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.GetSandboxRequestsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.GetSandboxRequestByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.ClearSandboxRequestsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Sandbox.ProcessSandboxRequestUseCase>();

        // Control Plane: Delivery Failure Simulator & Chaos Testing
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.CreateSimulatorRuleUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.GetSimulatorRulesUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.GetSimulatorRuleByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.UpdateSimulatorRuleUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.DeleteSimulatorRuleUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.ResetSimulatorRuleStepsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.ProcessSimulatorRequestUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.ExecuteAdHocSimulationUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.GetSimulatorExecutionsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.GetSimulatorExecutionByIdUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.ClearSimulatorExecutionsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.GetSimulatorStatsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Simulator.TestDispatchSimulatorUseCase>();

        // Control Plane: OpenTelemetry Observability, Metrics & Telemetry
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Observability.GetObservabilitySummaryUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Observability.GetMetricInstrumentsUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Observability.GetRecentCapturedSpansUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Observability.GenerateSyntheticTraceUseCase>();
        services.AddScoped<HookBridge.Application.ControlPlane.UseCases.Observability.GetPrometheusMetricsUseCase>();

        return services;
    }
}
