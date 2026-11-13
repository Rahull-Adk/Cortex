using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Subscriptions;
using Cortex.Domain.Enums;
using Cortex.Infrastructure.Auth.Settings;
using Cortex.Infrastructure.Billing.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Cortex.Infrastructure.Billing;

public class StripeBillingProvider : IBillingProvider
{
    private const string OrganizationIdKey = "organization_id";

    private readonly StripeSettings _settings;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<StripeBillingProvider> _logger;
    private readonly Lazy<StripeClient> _client;

    public StripeBillingProvider(
        IOptions<StripeSettings> settings,
        IOptions<FrontendSettings> frontend,
        ILogger<StripeBillingProvider> logger)
    {
        _settings = settings.Value;
        _frontendBaseUrl = (frontend.Value.BaseUrl ?? string.Empty).TrimEnd('/');
        _logger = logger;

        // Lazy so the API still starts in environments without Stripe configured.
        _client = new Lazy<StripeClient>(() => new StripeClient(Required(_settings.SecretKey, "Stripe:SecretKey")));
    }

    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default)
    {
        if (!_settings.Prices.TryGetValue(request.PlanCode, out var priceId) || string.IsNullOrWhiteSpace(priceId))
            throw new InvalidOperationException($"No Stripe price is configured for plan '{request.PlanCode}'.");

        var organizationId = request.OrganizationId.ToString();

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = request.BillingCustomerRef,
            ClientReferenceId = organizationId,
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { [OrganizationIdKey] = organizationId },
            },
            SuccessUrl = FrontendUrl(_settings.CheckoutSuccessPath, request.OrganizationId),
            CancelUrl = FrontendUrl(_settings.CheckoutCancelPath, request.OrganizationId),
        };

        var session = await new SessionService(_client.Value).CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(
        Guid organizationId, string billingCustomerRef, CancellationToken ct = default)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = billingCustomerRef,
            ReturnUrl = FrontendUrl(_settings.PortalReturnPath, organizationId),
        };

        var session = await new Stripe.BillingPortal.SessionService(_client.Value)
            .CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<Result<BillingWebhookEvent>> ParseWebhookAsync(
        string payload, string signature, CancellationToken ct = default)
    {
        var webhookSecret = Required(_settings.WebhookSecret, "Stripe:WebhookSecret");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return SubscriptionErrors.InvalidWebhook;
        }

        // Only the object id is taken from the event. State is fetched again so that
        // replayed or reordered deliveries always apply what Stripe holds right now.
        var objectId = (stripeEvent.Data.Object as IHasId)?.Id;
        if (string.IsNullOrEmpty(objectId))
            return new BillingWebhookEvent(null);

        string? subscriptionId;
        DateTime? paidThrough = null;

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                var session = await new SessionService(_client.Value).GetAsync(objectId, cancellationToken: ct);
                subscriptionId = session.SubscriptionId;
                break;

            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
                subscriptionId = objectId;
                break;

            case EventTypes.InvoicePaid:
            case EventTypes.InvoicePaymentFailed:
                var invoice = await new InvoiceService(_client.Value).GetAsync(objectId, cancellationToken: ct);
                subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
                if (invoice.Status == "paid" && invoice.Lines?.Data is { Count: > 0 } lines)
                    paidThrough = lines.Max(line => line.Period.End);
                break;

            default:
                return new BillingWebhookEvent(null);
        }

        if (string.IsNullOrEmpty(subscriptionId))
            return new BillingWebhookEvent(null);

        var subscription = await new Stripe.SubscriptionService(_client.Value)
            .GetAsync(subscriptionId, cancellationToken: ct);

        return new BillingWebhookEvent(ToSnapshot(subscription, paidThrough));
    }

    private BillingSubscriptionSnapshot? ToSnapshot(Subscription subscription, DateTime? paidThrough)
    {
        if (subscription.Metadata is null
            || !subscription.Metadata.TryGetValue(OrganizationIdKey, out var rawOrganizationId)
            || !Guid.TryParse(rawOrganizationId, out var organizationId))
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} carries no organization id.", subscription.Id);
            return null;
        }

        var item = subscription.Items?.Data?.FirstOrDefault();
        var planCode = _settings.Prices.FirstOrDefault(p => p.Value == item?.Price?.Id).Key;
        if (item is null || planCode is null)
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} uses a price that maps to no plan.", subscription.Id);
            return null;
        }

        return new BillingSubscriptionSnapshot(
            organizationId,
            planCode.ToLowerInvariant(),
            subscription.CustomerId,
            subscription.Id,
            MapStatus(subscription.Status),
            item.CurrentPeriodStart,
            item.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd || subscription.CancelAt is not null,
            paidThrough);
    }

    private static SubscriptionStatus MapStatus(string status) => status switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" => SubscriptionStatus.Cancelled,
        "incomplete" => SubscriptionStatus.Pending,
        _ => SubscriptionStatus.Expired,
    };

    private string FrontendUrl(string path, Guid organizationId) =>
        _frontendBaseUrl + path.Replace(StripeSettings.OrganizationIdPlaceholder, organizationId.ToString());

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"'{key}' is not configured.")
            : value;
}
