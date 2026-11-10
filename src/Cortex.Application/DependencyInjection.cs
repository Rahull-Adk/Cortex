using Cortex.Application.Auth;
using Cortex.Application.Authorization;
using Cortex.Application.Organizations;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Cortex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<OrganizationAuthorizer>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<MemberService>();
        services.AddValidatorsFromAssemblyContaining<AuthService>();
        return services;
    }
}
