using Cortex.Application.Common;
using Cortex.Domain.Enums;

namespace Cortex.Application.Interfaces;

public sealed record CheckoutSessionRequest(Guid OrganizationId, string PlanCode, string? BillingCustomerRef);

public sealed record BillingSubscriptionSnapshot(
    Guid OrganizationId,
    string PlanCode,
    string BillingCustomerRef,
    string BillingSubscriptionRef,
    SubscriptionStatus Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? PaidThroughUtc);

// Snapshot is null for events that do not concern a subscription.
public sealed record BillingWebhookEvent(BillingSubscriptionSnapshot? Snapshot);

public interface IBillingProvider
{
    Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct = default);
    Task<string> CreatePortalSessionAsync(Guid organizationId, string billingCustomerRef, CancellationToken ct = default);
    Task<Result<BillingWebhookEvent>> ParseWebhookAsync(string payload, string signature, CancellationToken ct = default);
}
