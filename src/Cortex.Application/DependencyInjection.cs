using Cortex.Application.Auth;
using Cortex.Application.Authorization;
using Cortex.Application.Organizations;
using Cortex.Application.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentValidation;

namespace Cortex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<OrganizationAuthorizer>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<MemberService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<EntitlementService>();
        services.AddValidatorsFromAssemblyContaining<AuthService>();
        return services;
    }
}
