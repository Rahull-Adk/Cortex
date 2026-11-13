using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations;
using Cortex.Application.Subscriptions;
using Cortex.Application.Subscriptions.Dtos;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Subscriptions;

public class SubscriptionServiceTests
{
    private readonly SubscriptionFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");
    private readonly User _admin = TestData.VerifiedUser("admin@example.com", "Admin");
    private readonly User _member = TestData.VerifiedUser("member@example.com", "Member");
    private readonly User _outsider = TestData.VerifiedUser("outsider@example.com", "Outsider");
    private readonly Organization _organization;

    public SubscriptionServiceTests()
    {
        _organization = _fx.HasOrganization(_owner);
        _fx.Orgs.HasMember(_organization, _admin, OrganizationRole.Admin);
        _fx.Orgs.HasMember(_organization, _member, OrganizationRole.Member);
        _fx.Orgs.HasUser(_outsider);
    }

    private SubscriptionService Service => _fx.SubscriptionService;

    [Fact]
    public async Task ListPlansAsync_ReturnsCatalogWithLimits()
    {
        var plans = await Service.ListPlansAsync();

        Assert.Equal(["free", "starter", "growth"], plans.Select(p => p.Code));
        var starter = plans[1];
        Assert.Equal(99_000L, starter.PriceMinor);
        Assert.Equal("thb", starter.Currency);
        Assert.Equal(500, starter.AiAnswersPerPeriod);
        Assert.Equal(100, starter.MaxDocuments);
        Assert.Equal(2 * SubscriptionFixture.Gigabyte, starter.MaxStorageBytes);
    }

    [Fact]
    public async Task GetAsync_NeverSubscribed_ReturnsFreePlanWithoutAccess()
    {
        var result = await Service.GetAsync(_owner.Id, _organization.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("free", result.Value.Plan.Code);
        Assert.Null(result.Value.Status);
        Assert.False(result.Value.HasPaidAccess);
        Assert.Equal(new UsageDto(0, 0, 0), result.Value.Usage);
    }

    [Fact]
    public async Task GetAsync_ActiveSubscription_ReturnsPlanPeriodAndUsage()
    {
        var subscription = _fx.HasSubscription(_organization, _fx.Growth);
        await _fx.EntitlementService.TryConsumeAiAnswerAsync(_organization.Id);
        await _fx.EntitlementService.TryReserveUploadAsync(_organization.Id, 4_096);

        var result = await Service.GetAsync(_admin.Id, _organization.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("growth", result.Value.Plan.Code);
        Assert.Equal(SubscriptionStatus.Active, result.Value.Status);
        Assert.True(result.Value.HasPaidAccess);
        Assert.Equal(subscription.CurrentPeriodEnd, result.Value.CurrentPeriodEnd);
        Assert.Equal(new UsageDto(1, 1, 4_096), result.Value.Usage);
    }

    [Fact]
    public async Task GetAsync_CancelledAfterPaidPeriod_ReportsNoAccessButKeepsPlan()
    {
        _fx.HasSubscription(_organization, _fx.Starter, SubscriptionStatus.Cancelled, paidThrough: _fx.Now.AddDays(-1));

        var result = await Service.GetAsync(_owner.Id, _organization.Id);

        Assert.Equal("starter", result.Value.Plan.Code);
        Assert.Equal(SubscriptionStatus.Cancelled, result.Value.Status);
        Assert.False(result.Value.HasPaidAccess);
    }

    [Fact]
    public async Task GetAsync_Member_IsForbidden()
    {
        var result = await Service.GetAsync(_member.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetAsync_NonMember_GetsNotFound()
    {
        var result = await Service.GetAsync(_outsider.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task StartCheckoutAsync_Owner_ReturnsCheckoutUrlForPlan()
    {
        var result = await Service.StartCheckoutAsync(_owner.Id, _organization.Id, new StartCheckoutRequest(" Starter "));

        Assert.True(result.IsSuccess);
        Assert.Equal("https://billing.test/checkout", result.Value.Url);
        var request = Assert.Single(_fx.CheckoutRequests);
        Assert.Equal(new CheckoutSessionRequest(_organization.Id, "starter", null), request);
    }

    [Fact]
    public async Task StartCheckoutAsync_AfterCancellation_ReusesBillingCustomer()
    {
        _fx.HasSubscription(_organization, _fx.Starter, SubscriptionStatus.Cancelled);

        var result = await Service.StartCheckoutAsync(_owner.Id, _organization.Id, new StartCheckoutRequest("growth"));

        Assert.True(result.IsSuccess);
        Assert.Equal("cus_1", Assert.Single(_fx.CheckoutRequests).BillingCustomerRef);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.PastDue)]
    public async Task StartCheckoutAsync_LiveSubscription_ReturnsConflict(SubscriptionStatus status)
    {
        _fx.HasSubscription(_organization, _fx.Starter, status);

        var result = await Service.StartCheckoutAsync(_owner.Id, _organization.Id, new StartCheckoutRequest("growth"));

        Assert.Equal(SubscriptionErrors.AlreadySubscribed, result.Error);
        Assert.Empty(_fx.CheckoutRequests);
    }

    [Fact]
    public async Task StartCheckoutAsync_Admin_IsForbidden()
    {
        var result = await Service.StartCheckoutAsync(_admin.Id, _organization.Id, new StartCheckoutRequest("starter"));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Empty(_fx.CheckoutRequests);
    }

    [Fact]
    public async Task StartCheckoutAsync_FreePlan_IsNotPurchasable()
    {
        var result = await Service.StartCheckoutAsync(_owner.Id, _organization.Id, new StartCheckoutRequest("free"));

        Assert.Equal(SubscriptionErrors.PlanNotPurchasable, result.Error);
    }

    [Fact]
    public async Task StartCheckoutAsync_UnknownPlan_ReturnsNotFound()
    {
        var result = await Service.StartCheckoutAsync(_owner.Id, _organization.Id, new StartCheckoutRequest("enterprise"));

        Assert.Equal(SubscriptionErrors.PlanNotFound, result.Error);
    }

    [Fact]
    public async Task OpenPortalAsync_Owner_ReturnsPortalUrl()
    {
        _fx.HasSubscription(_organization, _fx.Starter);

        var result = await Service.OpenPortalAsync(_owner.Id, _organization.Id);

        Assert.Equal("https://billing.test/portal", result.Value.Url);
    }

    [Fact]
    public async Task OpenPortalAsync_NeverSubscribed_ReturnsNotFound()
    {
        var result = await Service.OpenPortalAsync(_owner.Id, _organization.Id);

        Assert.Equal(SubscriptionErrors.NoBillingAccount, result.Error);
    }

    [Fact]
    public async Task OpenPortalAsync_Admin_IsForbidden()
    {
        _fx.HasSubscription(_organization, _fx.Starter);

        var result = await Service.OpenPortalAsync(_admin.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task HandleWebhookAsync_InvalidSignature_ReturnsValidationAndSavesNothing()
    {
        _fx.WebhookIsRejected();

        var result = await Service.HandleWebhookAsync("{}", "bad");

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(0, _fx.Orgs.SaveCount);
    }

    [Fact]
    public async Task HandleWebhookAsync_UnrelatedEvent_IsAcknowledgedWithoutSaving()
    {
        _fx.WebhookDelivers(null);

        var result = await Service.HandleWebhookAsync("{}", "sig");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _fx.Orgs.SaveCount);
    }

    [Fact]
    public async Task HandleWebhookAsync_PaymentConfirmed_ActivatesSubscriptionAndEntitlements()
    {
        var paidThrough = _fx.Now.AddDays(29);
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter, paidThrough: paidThrough));

        var result = await Service.HandleWebhookAsync("{}", "sig");

        Assert.True(result.IsSuccess);
        var stored = _fx.StoredSubscription(_organization.Id)!;
        Assert.Equal(SubscriptionStatus.Active, stored.Status);
        Assert.Equal(_fx.Starter.Id, stored.PlanId);
        Assert.Equal("cus_1", stored.BillingCustomerRef);
        Assert.Equal("sub_1", stored.BillingSubscriptionRef);
        Assert.Equal(paidThrough, stored.PaidThroughUtc);
        Assert.Equal(1, _fx.Orgs.SaveCount);
        Assert.True((await _fx.EntitlementService.TryConsumeAiAnswerAsync(_organization.Id)).IsSuccess);
    }

    [Fact]
    public async Task HandleWebhookAsync_ReplayedEvent_LeavesSameState()
    {
        var paidThrough = _fx.Now.AddDays(29);
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter, paidThrough: paidThrough));
        await Service.HandleWebhookAsync("{}", "sig");
        var first = _fx.StoredSubscription(_organization.Id)!;

        await Service.HandleWebhookAsync("{}", "sig");

        var second = _fx.StoredSubscription(_organization.Id)!;
        Assert.Same(first, second);
        Assert.Equal(SubscriptionStatus.Active, second.Status);
        Assert.Equal(paidThrough, second.PaidThroughUtc);
    }

    [Fact]
    public async Task HandleWebhookAsync_EventWithoutPayment_DoesNotShortenPaidPeriod()
    {
        var subscription = _fx.HasSubscription(_organization, _fx.Starter);
        var paidThrough = subscription.PaidThroughUtc;
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter, SubscriptionStatus.PastDue));

        await Service.HandleWebhookAsync("{}", "sig");

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(paidThrough, subscription.PaidThroughUtc);
    }

    [Fact]
    public async Task HandleWebhookAsync_CancelAtPeriodEnd_KeepsAccessAndFlagsCancellation()
    {
        var subscription = _fx.HasSubscription(_organization, _fx.Starter);
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter, cancelAtPeriodEnd: true));

        await Service.HandleWebhookAsync("{}", "sig");

        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.True((await _fx.EntitlementService.CheckIngestionAsync(_organization.Id)).IsSuccess);
    }

    [Fact]
    public async Task HandleWebhookAsync_PlanChangedInPortal_AppliesNewPlanLimits()
    {
        var subscription = _fx.HasSubscription(_organization, _fx.Starter);
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Growth));

        await Service.HandleWebhookAsync("{}", "sig");

        Assert.Equal(_fx.Growth.Id, subscription.PlanId);
        Assert.Equal(2_500, (await Service.GetAsync(_owner.Id, _organization.Id)).Value.Plan.AiAnswersPerPeriod);
    }

    [Fact]
    public async Task HandleWebhookAsync_LateEventFromReplacedSubscription_IsIgnored()
    {
        var subscription = _fx.HasSubscription(_organization, _fx.Growth, subscriptionRef: "sub_new");
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter, SubscriptionStatus.Cancelled, "sub_old"));

        var result = await Service.HandleWebhookAsync("{}", "sig");

        Assert.True(result.IsSuccess);
        Assert.Equal("sub_new", subscription.BillingSubscriptionRef);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(_fx.Growth.Id, subscription.PlanId);
        Assert.Equal(0, _fx.Orgs.SaveCount);
    }

    [Fact]
    public async Task HandleWebhookAsync_NewSubscriptionAfterExpiry_ReplacesOldOne()
    {
        var subscription = _fx.HasSubscription(
            _organization, _fx.Starter, SubscriptionStatus.Cancelled, _fx.Now.AddDays(-3), "sub_old");
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Growth, subscriptionRef: "sub_new"));

        await Service.HandleWebhookAsync("{}", "sig");

        Assert.Equal("sub_new", subscription.BillingSubscriptionRef);
        Assert.True(subscription.HasPaidAccess(_fx.Now));
    }

    [Fact]
    public async Task HandleWebhookAsync_UnknownBusiness_IsAcknowledgedWithoutSaving()
    {
        var elsewhere = Organization.Create("Elsewhere", "elsewhere", _owner.Id);
        _fx.WebhookDelivers(_fx.Snapshot(elsewhere, _fx.Starter));

        var result = await Service.HandleWebhookAsync("{}", "sig");

        Assert.True(result.IsSuccess);
        Assert.Null(_fx.StoredSubscription(elsewhere.Id));
        Assert.Equal(0, _fx.Orgs.SaveCount);
    }

    [Fact]
    public async Task HandleWebhookAsync_ConcurrentDelivery_ReturnsConflictSoProviderRetries()
    {
        _fx.Orgs.SaveHitsUniqueConstraint();
        _fx.WebhookDelivers(_fx.Snapshot(_organization, _fx.Starter));

        var result = await Service.HandleWebhookAsync("{}", "sig");

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(1, _fx.Orgs.DiscardCount);
    }
}
