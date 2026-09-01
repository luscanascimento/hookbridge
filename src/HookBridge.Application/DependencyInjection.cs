using FluentValidation;
using HookBridge.Application.Abstractions;
using HookBridge.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
