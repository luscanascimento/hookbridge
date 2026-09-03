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

        return services;
    }
}
