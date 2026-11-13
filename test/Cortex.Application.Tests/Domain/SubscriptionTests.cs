using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Domain;

public class SubscriptionTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

    private readonly Plan _starter = Plan.Create("Starter", "Starter", 99_000, "THB", 500, 100, 1_000);
    private readonly Plan _free = Plan.Create("free", "Free", 0, "thb", 0, 0, 0);

    [Fact]
    public void PlanCreate_NormalizesCodeAndCurrency()
    {
        Assert.Equal("starter", _starter.Code);
        Assert.Equal("thb", _starter.Currency);
        Assert.True(_starter.IsPaid);
        Assert.False(_free.IsPaid);
    }

    [Fact]
    public void PlanCreate_NegativeLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan.Create("x", "X", 0, "thb", -1, 0, 0));
    }

    [Fact]
    public void Create_StartsPendingWithoutAccess()
    {
        var subscription = OrganizationSubscription.Create(Guid.NewGuid(), _starter);

        Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
        Assert.False(subscription.HasPaidAccess(Now));
        Assert.False(subscription.IsLive);
    }

    [Fact]
    public void Create_WithoutOrganization_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrganizationSubscription.Create(Guid.Empty, _starter));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, false, true)]
    [InlineData(SubscriptionStatus.Active, true, true)]
    [InlineData(SubscriptionStatus.PastDue, true, true)]
    [InlineData(SubscriptionStatus.PastDue, false, false)]
    [InlineData(SubscriptionStatus.Cancelled, true, true)]
    [InlineData(SubscriptionStatus.Cancelled, false, false)]
    [InlineData(SubscriptionStatus.Expired, false, false)]
    [InlineData(SubscriptionStatus.Pending, false, false)]
    public void HasPaidAccess_DependsOnStatusAndPaidPeriod(SubscriptionStatus status, bool paidPeriodRunning, bool expected)
    {
        var subscription = Subscription(status, paidPeriodRunning ? Now.AddDays(1) : Now.AddDays(-1));

        Assert.Equal(expected, subscription.HasPaidAccess(Now));
    }

    [Fact]
    public void HasPaidAccess_AtExactEndOfPaidPeriod_IsLocked()
    {
        var subscription = Subscription(SubscriptionStatus.Cancelled, Now);

        Assert.False(subscription.HasPaidAccess(Now));
    }

    [Fact]
    public void ApplyProviderState_NeverMovesPaidThroughBackwards()
    {
        var subscription = Subscription(SubscriptionStatus.Active, Now.AddDays(30));

        subscription.ApplyProviderState(_starter, SubscriptionStatus.Active, "cus_1", "sub_1", Now, Now.AddDays(30), false, Now.AddDays(2));
        subscription.ApplyProviderState(_starter, SubscriptionStatus.Active, "cus_1", "sub_1", Now, Now.AddDays(30), false, null);

        Assert.Equal(Now.AddDays(30), subscription.PaidThroughUtc);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.Pending, false)]
    public void IsLive_OnlyWhileProviderStillBills(SubscriptionStatus status, bool expected)
    {
        Assert.Equal(expected, Subscription(status, Now.AddDays(1)).IsLive);
    }

    [Theory]
    [InlineData("sub_1", SubscriptionStatus.Cancelled, true)]
    [InlineData("sub_2", SubscriptionStatus.Active, true)]
    [InlineData("sub_2", SubscriptionStatus.Cancelled, false)]
    [InlineData("sub_2", SubscriptionStatus.Expired, false)]
    [InlineData("sub_2", SubscriptionStatus.Pending, false)]
    public void Accepts_OtherSubscriptionOnlyWhenItIsActive(string incomingRef, SubscriptionStatus incoming, bool expected)
    {
        var subscription = Subscription(SubscriptionStatus.Active, Now.AddDays(1));

        Assert.Equal(expected, subscription.Accepts(incomingRef, incoming));
    }

    [Fact]
    public void Accepts_AnythingBeforeFirstProviderState()
    {
        var subscription = OrganizationSubscription.Create(Guid.NewGuid(), _starter);

        Assert.True(subscription.Accepts("sub_9", SubscriptionStatus.Pending));
    }

    private OrganizationSubscription Subscription(SubscriptionStatus status, DateTime paidThrough)
    {
        var subscription = OrganizationSubscription.Create(Guid.NewGuid(), _starter);
        subscription.ApplyProviderState(_starter, status, "cus_1", "sub_1", Now.AddDays(-10), Now.AddDays(20), false, paidThrough);
        return subscription;
    }
}
