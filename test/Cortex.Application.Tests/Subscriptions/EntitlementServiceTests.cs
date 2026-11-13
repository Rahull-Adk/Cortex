using Cortex.Application.Common;
using Cortex.Application.Organizations.Dtos;
using Cortex.Application.Subscriptions;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Subscriptions;

public class EntitlementServiceTests
{
    private const long Megabyte = 1024 * 1024;

    private readonly SubscriptionFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");

    private EntitlementService Service => _fx.EntitlementService;

    [Fact]
    public async Task FreeBusiness_PaidCapabilities_RequireSubscription()
    {
        var organization = _fx.HasOrganization(_owner);

        var upload = await Service.TryReserveUploadAsync(organization.Id, Megabyte);
        var ingestion = await Service.CheckIngestionAsync(organization.Id);
        var answer = await Service.TryConsumeAiAnswerAsync(organization.Id);

        AssertSubscriptionRequired(upload);
        AssertSubscriptionRequired(ingestion);
        AssertSubscriptionRequired(answer);
    }

    [Fact]
    public async Task FreeBusiness_CanStillBeCreatedAndManageMembers()
    {
        _fx.Orgs.HasUser(_owner);

        var created = await _fx.Orgs.OrganizationService.CreateAsync(
            _owner.Id, new CreateOrganizationRequest("Shwe Store", "shwe-store"));
        Assert.True(created.IsSuccess);

        var organization = _fx.HasOrganization(_owner);
        var invited = await _fx.Orgs.InvitationService.InviteAsync(
            _owner.Id, organization.Id, new InviteMemberRequest("staff@example.com", OrganizationRole.Member));

        Assert.True(invited.IsSuccess);
        AssertSubscriptionRequired(await Service.TryConsumeAiAnswerAsync(organization.Id));
    }

    [Fact]
    public async Task PendingSubscription_DoesNotGrantAccess()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter, SubscriptionStatus.Pending, paidThrough: DateTime.MinValue);

        AssertSubscriptionRequired(await Service.CheckIngestionAsync(organization.Id));
    }

    [Theory]
    [InlineData("starter", 500)]
    [InlineData("growth", 2_500)]
    public async Task ActiveSubscription_AllowsAnswersUpToPlanAllowanceThenBlocks(string planCode, int allowance)
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, planCode == "starter" ? _fx.Starter : _fx.Growth);

        for (var i = 0; i < allowance; i++)
            Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsSuccess);

        var blocked = await Service.TryConsumeAiAnswerAsync(organization.Id);

        Assert.Equal(SubscriptionErrors.AiAnswerQuotaExceeded, blocked.Error);
        Assert.Equal(ErrorType.PaymentRequired, blocked.Error!.Type);
        Assert.Equal(SubscriptionErrors.QuotaExceededCode, blocked.Error.Code);
    }

    [Fact]
    public async Task AnswerQuotaExhausted_UploadsStillWorkAndStoredKnowledgeIsKept()
    {
        var organization = _fx.HasOrganization(_owner);
        var subscription = _fx.HasSubscription(organization, _fx.Starter);
        Assert.True((await Service.TryReserveUploadAsync(organization.Id, Megabyte)).IsSuccess);

        for (var i = 0; i < _fx.Starter.AiAnswersPerPeriod; i++)
            await Service.TryConsumeAiAnswerAsync(organization.Id);
        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsFailure);

        Assert.True((await Service.TryReserveUploadAsync(organization.Id, Megabyte)).IsSuccess);
        Assert.True((await Service.CheckIngestionAsync(organization.Id)).IsSuccess);

        var storage = await _fx.Usage.GetStorageAsync(organization.Id);
        Assert.Equal(2, storage.DocumentCount);
        Assert.Equal(2 * Megabyte, storage.StorageBytes);
        Assert.Equal(500, await _fx.Usage.GetAiAnswersUsedAsync(organization.Id, subscription.CurrentPeriodStart!.Value));
    }

    [Fact]
    public async Task DocumentLimitReached_BlocksUploadsButNotAnswers()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter);

        for (var i = 0; i < _fx.Starter.MaxDocuments; i++)
            Assert.True((await Service.TryReserveUploadAsync(organization.Id, 1)).IsSuccess);

        var blocked = await Service.TryReserveUploadAsync(organization.Id, 1);

        Assert.Equal(SubscriptionErrors.DocumentQuotaExceeded, blocked.Error);
        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsSuccess);
        Assert.Equal(100, (await _fx.Usage.GetStorageAsync(organization.Id)).DocumentCount);
    }

    [Fact]
    public async Task StorageLimitReached_BlocksUploadThatWouldExceedIt()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter);
        Assert.True((await Service.TryReserveUploadAsync(organization.Id, 2 * SubscriptionFixture.Gigabyte - 10)).IsSuccess);

        var blocked = await Service.TryReserveUploadAsync(organization.Id, 11);
        var fits = await Service.TryReserveUploadAsync(organization.Id, 10);

        Assert.Equal(SubscriptionErrors.StorageQuotaExceeded, blocked.Error);
        Assert.True(fits.IsSuccess);
    }

    [Fact]
    public async Task GrowthPlan_AllowsMoreDocumentsThanStarter()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Growth);

        for (var i = 0; i < 101; i++)
            Assert.True((await Service.TryReserveUploadAsync(organization.Id, 1)).IsSuccess);
    }

    [Fact]
    public async Task ReleaseUpload_FreesDocumentSlotAndBytes()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter);
        await Service.TryReserveUploadAsync(organization.Id, Megabyte);

        await Service.ReleaseUploadAsync(organization.Id, Megabyte);

        var storage = await _fx.Usage.GetStorageAsync(organization.Id);
        Assert.Equal(0, storage.DocumentCount);
        Assert.Equal(0L, storage.StorageBytes);
    }

    [Fact]
    public async Task RefundAiAnswer_ReturnsUnitToAllowance()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter);
        for (var i = 0; i < _fx.Starter.AiAnswersPerPeriod; i++)
            await Service.TryConsumeAiAnswerAsync(organization.Id);

        await Service.RefundAiAnswerAsync(organization.Id);

        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsSuccess);
        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsFailure);
    }

    [Fact]
    public async Task NewBillingPeriod_ResetsAnswerAllowance()
    {
        var organization = _fx.HasOrganization(_owner);
        var subscription = _fx.HasSubscription(organization, _fx.Starter);
        for (var i = 0; i < _fx.Starter.AiAnswersPerPeriod; i++)
            await Service.TryConsumeAiAnswerAsync(organization.Id);
        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsFailure);

        var nextStart = subscription.CurrentPeriodEnd!.Value;
        subscription.ApplyProviderState(
            _fx.Starter, SubscriptionStatus.Active, "cus_1", "sub_1", nextStart, nextStart.AddMonths(1), false, nextStart.AddMonths(1));

        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsSuccess);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.PastDue)]
    public async Task CancelledOrUnpaid_KeepsAccessUntilPaidPeriodEndsThenLocks(SubscriptionStatus status)
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter, status, paidThrough: _fx.Now.AddDays(5));
        await Service.TryReserveUploadAsync(organization.Id, Megabyte);

        Assert.True((await Service.TryConsumeAiAnswerAsync(organization.Id)).IsSuccess);
        Assert.True((await Service.CheckIngestionAsync(organization.Id)).IsSuccess);

        _fx.Clock.Advance(TimeSpan.FromDays(5));

        AssertSubscriptionRequired(await Service.TryConsumeAiAnswerAsync(organization.Id));
        AssertSubscriptionRequired(await Service.TryReserveUploadAsync(organization.Id, Megabyte));
        AssertSubscriptionRequired(await Service.CheckIngestionAsync(organization.Id));

        Assert.Equal(1, (await _fx.Usage.GetStorageAsync(organization.Id)).DocumentCount);
    }

    [Fact]
    public async Task ExpiredSubscription_BusinessAndMembersRemainAccessible()
    {
        var organization = _fx.HasOrganization(_owner);
        _fx.HasSubscription(organization, _fx.Starter, SubscriptionStatus.Expired, paidThrough: _fx.Now.AddDays(-1));

        AssertSubscriptionRequired(await Service.CheckIngestionAsync(organization.Id));
        Assert.True((await _fx.Orgs.OrganizationService.GetAsync(_owner.Id, organization.Id)).IsSuccess);
        Assert.True((await _fx.Orgs.MemberService.ListAsync(_owner.Id, organization.Id)).IsSuccess);
    }

    private static void AssertSubscriptionRequired(Result result)
    {
        Assert.Equal(SubscriptionErrors.SubscriptionRequired, result.Error);
        Assert.Equal(ErrorType.PaymentRequired, result.Error!.Type);
        Assert.Equal(SubscriptionErrors.SubscriptionRequiredCode, result.Error.Code);
    }
}
