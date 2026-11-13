using Cortex.Domain.Enums;

namespace Cortex.Domain.Entities;

public class OrganizationSubscription
{
    private OrganizationSubscription() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public string? BillingCustomerRef { get; private set; }
    public string? BillingSubscriptionRef { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? PaidThroughUtc { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Plan Plan { get; private set; } = default!;

    // Still billed by the provider, so a second checkout would double-charge.
    public bool IsLive => Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public static OrganizationSubscription Create(Guid organizationId, Plan plan)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("A subscription requires an organization.", nameof(organizationId));
        ArgumentNullException.ThrowIfNull(plan);

        var now = DateTime.UtcNow;

        return new OrganizationSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlanId = plan.Id,
            Plan = plan,
            Status = SubscriptionStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool HasPaidAccess(DateTime utcNow) =>
        Status == SubscriptionStatus.Active || (PaidThroughUtc is { } paidThrough && utcNow < paidThrough);

    // A late event from an older subscription must not overwrite the one that replaced it.
    public bool Accepts(string billingSubscriptionRef, SubscriptionStatus incomingStatus) =>
        BillingSubscriptionRef is null
        || BillingSubscriptionRef == billingSubscriptionRef
        || incomingStatus == SubscriptionStatus.Active;

    public void ApplyProviderState(
        Plan plan,
        SubscriptionStatus status,
        string billingCustomerRef,
        string billingSubscriptionRef,
        DateTime? currentPeriodStart,
        DateTime? currentPeriodEnd,
        bool cancelAtPeriodEnd,
        DateTime? paidThroughUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(billingCustomerRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(billingSubscriptionRef);

        PlanId = plan.Id;
        Plan = plan;
        Status = status;
        BillingCustomerRef = billingCustomerRef;
        BillingSubscriptionRef = billingSubscriptionRef;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        CancelAtPeriodEnd = cancelAtPeriodEnd;

        // Only ever extended, so replayed or reordered payment events cannot shorten paid access.
        if (paidThroughUtc is { } paidThrough && (PaidThroughUtc is null || paidThrough > PaidThroughUtc))
            PaidThroughUtc = paidThrough;

        UpdatedAt = DateTime.UtcNow;
    }
}
