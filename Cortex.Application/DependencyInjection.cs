using Cortex.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Cortex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddValidatorsFromAssemblyContaining<AuthService>();
        return services;
    }
}
