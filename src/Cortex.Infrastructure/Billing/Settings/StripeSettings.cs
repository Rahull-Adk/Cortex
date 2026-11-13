namespace Cortex.Infrastructure.Billing.Settings;

public class StripeSettings
{
    public const string OrganizationIdPlaceholder = "{organizationId}";

    public string SecretKey { get; set; } = default!;
    public string WebhookSecret { get; set; } = default!;

    public Dictionary<string, string> Prices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string CheckoutSuccessPath { get; set; } = "/organizations/{organizationId}/billing?checkout=success";
    public string CheckoutCancelPath { get; set; } = "/organizations/{organizationId}/billing?checkout=cancelled";
    public string PortalReturnPath { get; set; } = "/organizations/{organizationId}/billing";
}
