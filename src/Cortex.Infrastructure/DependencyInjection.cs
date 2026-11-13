using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Auth;
using Cortex.Infrastructure.Auth.Settings;
using Cortex.Infrastructure.Billing;
using Cortex.Infrastructure.Billing.Settings;
using Cortex.Infrastructure.Persistence;
using Cortex.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cortex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CortexDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<GoogleAuthSettings>(configuration.GetSection("GoogleAuth"));
        services.Configure<BrevoSettings>(configuration.GetSection("Brevo"));
        services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));
        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationMembershipRepository, OrganizationMembershipRepository>();
        services.AddScoped<IOrganizationInvitationRepository, OrganizationInvitationRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IOrganizationSubscriptionRepository, OrganizationSubscriptionRepository>();
        services.AddScoped<IUsageRepository, UsageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IExternalIdentityTokenValidator, GoogleIdentityTokenValidator>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddSingleton<IBillingProvider, StripeBillingProvider>();

        services.AddHttpClient("Brevo", (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<BrevoSettings>>().Value;
            client.BaseAddress = new Uri("https://api.brevo.com");
            client.DefaultRequestHeaders.Add("api-key", settings.ApiKey);
        });

        return services;
    }
}

