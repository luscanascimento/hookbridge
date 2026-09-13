using HookBridge.Application.Abstractions;
using HookBridge.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Infrastructure;

internal static class SecurityExtensions
{
    internal static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 2. Cryptographic & Auth Services with Startup Validation
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<WebhookEncryptionOptions>()
            .BindConfiguration(WebhookEncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SsrfOptions>()
            .BindConfiguration(SsrfOptions.SectionName);

        services.AddOptions<HookBridge.Infrastructure.Integration.EventFlowOptions>()
            .BindConfiguration(HookBridge.Infrastructure.Integration.EventFlowOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HookBridge.Infrastructure.Resilience.ResilienceOptions>()
            .BindConfiguration(HookBridge.Infrastructure.Resilience.ResilienceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecretEncryptor, AesSecretEncryptor>();
        services.AddSingleton<IApiKeyGenerator, KeyGenerator>();
        services.AddSingleton<ISsrfGuard, SsrfGuard>();
        services.AddSingleton<IWebhookSigner, WebhookSigner>();
        services.AddSingleton<HookBridge.Infrastructure.Resilience.IHttpResiliencePipelineProvider, HookBridge.Infrastructure.Resilience.HttpResiliencePipelineProvider>();

        return services;
    }
}
