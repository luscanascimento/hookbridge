using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Auth.UseCases;
using HookBridge.Application.Common;
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

        return services;
    }
}
