using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Subscriptions;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Moq;

namespace Cortex.Application.Tests.Support;

internal sealed class SubscriptionFixture
{
    public const long Gigabyte = 1024L * 1024 * 1024;

    private readonly Dictionary<Guid, OrganizationSubscription> _subscriptions = [];
    private readonly HashSet<Guid> _organizationIds = [];

    public OrganizationFixture Orgs { get; } = new();
    public FakeClock Clock { get; } = new();
    public InMemoryUsageRepository Usage { get; } = new();
    public Mock<IBillingProvider> Billing { get; } = new();
    public Mock<IPlanRepository> Plans { get; } = new();
    public Mock<IOrganizationSubscriptionRepository> Subscriptions { get; } = new();

    public Plan Free { get; } = Plan.Create(Plan.FreeCode, "Free", 0, "thb", 0, 0, 0, 0);
    public Plan Starter { get; } = Plan.Create("starter", "Starter", 99_000, "thb", 500, 100, 2 * Gigabyte, 1);
    public Plan Growth { get; } = Plan.Create("growth", "Growth", 249_000, "thb", 2_500, 500, 10 * Gigabyte, 2);

    public List<CheckoutSessionRequest> CheckoutRequests { get; } = [];

    public SubscriptionService SubscriptionService { get; }
    public EntitlementService EntitlementService { get; }

    public SubscriptionFixture()
    {
        Plan[] catalog = [Free, Starter, Growth];

        Plans.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        Plans.Setup(r => r.FindByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => catalog.FirstOrDefault(p => p.Code == code));

        Subscriptions.Setup(r => r.FindByOrganizationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, CancellationToken _) => _subscriptions.GetValueOrDefault(organizationId));
        Subscriptions.Setup(r => r.Add(It.IsAny<OrganizationSubscription>()))
            .Callback<OrganizationSubscription>(s => _subscriptions[s.OrganizationId] = s);

        Orgs.Organizations.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, CancellationToken _) => _organizationIds.Contains(organizationId));

        Billing.Setup(b => b.CreateCheckoutSessionAsync(It.IsAny<CheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CheckoutSessionRequest, CancellationToken>((request, _) => CheckoutRequests.Add(request))
            .ReturnsAsync("https://billing.test/checkout");
        Billing.Setup(b => b.CreatePortalSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://billing.test/portal");

        var authorizer = new OrganizationAuthorizer(Orgs.Memberships.Object);
        SubscriptionService = new SubscriptionService(
            Plans.Object, Subscriptions.Object, Orgs.Organizations.Object, Usage, Billing.Object,
            Orgs.UnitOfWork.Object, authorizer, Clock);
        EntitlementService = new EntitlementService(Subscriptions.Object, Usage, Clock);
    }

    public DateTime Now => Clock.UtcNow;

    public Organization HasOrganization(User owner)
    {
        var organization = Orgs.HasOrganization(owner);
        _organizationIds.Add(organization.Id);
        return organization;
    }

    public OrganizationSubscription? StoredSubscription(Guid organizationId) =>
        _subscriptions.GetValueOrDefault(organizationId);

    // The current period started ten days ago and is paid through its end unless stated otherwise.
    public OrganizationSubscription HasSubscription(
        Organization organization,
        Plan plan,
        SubscriptionStatus status = SubscriptionStatus.Active,
        DateTime? paidThrough = null,
        string subscriptionRef = "sub_1")
    {
        var periodStart = Now.AddDays(-10);
        var periodEnd = Now.AddDays(20);

        var subscription = OrganizationSubscription.Create(organization.Id, plan);
        subscription.ApplyProviderState(
            plan, status, "cus_1", subscriptionRef, periodStart, periodEnd, false, paidThrough ?? periodEnd);

        _subscriptions[organization.Id] = subscription;
        return subscription;
    }

    public BillingSubscriptionSnapshot Snapshot(
        Organization organization,
        Plan plan,
        SubscriptionStatus status = SubscriptionStatus.Active,
        string subscriptionRef = "sub_1",
        DateTime? paidThrough = null,
        bool cancelAtPeriodEnd = false) =>
        new(organization.Id, plan.Code, "cus_1", subscriptionRef, status,
            Now.AddDays(-1), Now.AddDays(29), cancelAtPeriodEnd, paidThrough);

    public void WebhookDelivers(BillingSubscriptionSnapshot? snapshot) =>
        Billing.Setup(b => b.ParseWebhookAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BillingWebhookEvent>.Success(new BillingWebhookEvent(snapshot)));

    public void WebhookIsRejected() =>
        Billing.Setup(b => b.ParseWebhookAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BillingWebhookEvent>.Failure(SubscriptionErrors.InvalidWebhook));
}

internal sealed class FakeClock : TimeProvider
{
    public DateTime UtcNow { get; private set; } = DateTime.UtcNow;

    public override DateTimeOffset GetUtcNow() => new(UtcNow);

    public void Advance(TimeSpan by) => UtcNow += by;
}

// Mirrors the conditional updates of the real repository without a database.
internal sealed class InMemoryUsageRepository : IUsageRepository
{
    private readonly Dictionary<(Guid OrganizationId, DateTime PeriodStart), int> _answers = [];
    private readonly Dictionary<Guid, StorageUsage> _storage = [];

    public Task<bool> TryConsumeAiAnswerAsync(
        Guid organizationId, DateTime periodStart, DateTime periodEnd, int limit, CancellationToken ct = default)
    {
        var used = _answers.GetValueOrDefault((organizationId, periodStart));
        if (used >= limit)
            return Task.FromResult(false);

        _answers[(organizationId, periodStart)] = used + 1;
        return Task.FromResult(true);
    }

    public Task RefundAiAnswerAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default)
    {
        var used = _answers.GetValueOrDefault((organizationId, periodStart));
        if (used > 0)
            _answers[(organizationId, periodStart)] = used - 1;
        return Task.CompletedTask;
    }

    public Task<int> GetAiAnswersUsedAsync(Guid organizationId, DateTime periodStart, CancellationToken ct = default) =>
        Task.FromResult(_answers.GetValueOrDefault((organizationId, periodStart)));

    public Task<bool> TryReserveStorageAsync(
        Guid organizationId, long bytes, int maxDocuments, long maxStorageBytes, CancellationToken ct = default)
    {
        var used = Current(organizationId);
        if (used.DocumentCount >= maxDocuments || used.StorageBytes + bytes > maxStorageBytes)
            return Task.FromResult(false);

        _storage[organizationId] = new StorageUsage(used.DocumentCount + 1, used.StorageBytes + bytes);
        return Task.FromResult(true);
    }

    public Task ReleaseStorageAsync(Guid organizationId, long bytes, CancellationToken ct = default)
    {
        var used = Current(organizationId);
        _storage[organizationId] = new StorageUsage(
            Math.Max(0, used.DocumentCount - 1), Math.Max(0, used.StorageBytes - bytes));
        return Task.CompletedTask;
    }

    public Task<StorageUsage> GetStorageAsync(Guid organizationId, CancellationToken ct = default) =>
        Task.FromResult(Current(organizationId));

    private StorageUsage Current(Guid organizationId) =>
        _storage.GetValueOrDefault(organizationId) ?? new StorageUsage(0, 0);
}
