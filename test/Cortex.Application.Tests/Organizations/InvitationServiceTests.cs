using Cortex.Application.Common;
using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Organizations;

public class InvitationServiceTests
{
    private const string StaffEmail = "staff@example.com";

    private readonly OrganizationFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");
    private readonly User _admin = TestData.VerifiedUser("admin@example.com", "Admin");
    private readonly User _member = TestData.VerifiedUser("member@example.com", "Member");
    private readonly Organization _organization;

    public InvitationServiceTests()
    {
        _organization = _fx.HasOrganization(_owner);
        _fx.HasMember(_organization, _admin, OrganizationRole.Admin);
        _fx.HasMember(_organization, _member, OrganizationRole.Member);
    }

    private InvitationService Service => _fx.InvitationService;

    private User ActorWithRole(OrganizationRole role) => role == OrganizationRole.Owner ? _owner : _admin;

    private async Task<string> InviteStaffAsync(OrganizationRole role = OrganizationRole.Member)
    {
        var result = await Service.InviteAsync(_owner.Id, _organization.Id, new InviteMemberRequest(StaffEmail, role));
        Assert.True(result.IsSuccess);
        return _fx.SentInvitations[^1].RawToken;
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Member)]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Admin)]
    public async Task InviteAsync_OwnerOrAdmin_SendsEmailWithoutAddingMember(OrganizationRole actorRole, OrganizationRole newRole)
    {
        var actor = ActorWithRole(actorRole);

        var result = await Service.InviteAsync(actor.Id, _organization.Id, new InviteMemberRequest(StaffEmail, newRole));

        Assert.True(result.IsSuccess);
        Assert.Equal(StaffEmail, result.Value.Email);
        Assert.Equal(newRole, result.Value.Role);
        Assert.Equal(actor.Name, result.Value.InvitedByName);
        Assert.Empty(_fx.AddedMemberships);

        var sent = Assert.Single(_fx.SentInvitations);
        Assert.Equal(StaffEmail, sent.Email);
        Assert.Equal(_organization.Name, sent.OrganizationName);
        Assert.Equal(actor.Name, sent.InviterName);
        Assert.Equal(newRole.ToString(), sent.RoleName);
        Assert.Equal(1, sent.SavesBeforeSend);
    }

    [Fact]
    public async Task InviteAsync_StoresOnlyHashOfEmailedToken()
    {
        var rawToken = await InviteStaffAsync();

        var invitation = Assert.Single(_fx.PendingInvitations);
        Assert.NotEqual(rawToken, invitation.TokenHash);
        Assert.Equal(TestData.Sha256(rawToken), invitation.TokenHash);
    }

    [Fact]
    public async Task InviteAsync_ExpiresAfterSevenDays()
    {
        var result = await Service.InviteAsync(
            _owner.Id, _organization.Id, new InviteMemberRequest(StaffEmail, OrganizationRole.Member));

        var lifetime = result.Value.ExpiresAt - result.Value.CreatedAt;
        Assert.InRange(lifetime, TimeSpan.FromDays(7) - TimeSpan.FromSeconds(5), TimeSpan.FromDays(7) + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InviteAsync_EmailWithCaseAndSpaces_IsNormalized()
    {
        var result = await Service.InviteAsync(
            _owner.Id, _organization.Id, new InviteMemberRequest("  Staff@Example.COM ", OrganizationRole.Member));

        Assert.Equal(StaffEmail, result.Value.Email);
        Assert.Equal(StaffEmail, Assert.Single(_fx.SentInvitations).Email);
    }

    [Fact]
    public async Task InviteAsync_Member_ReturnsForbiddenAndSendsNothing()
    {
        var result = await Service.InviteAsync(
            _member.Id, _organization.Id, new InviteMemberRequest(StaffEmail, OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Empty(_fx.SentInvitations);
        Assert.Empty(_fx.PendingInvitations);
    }

    [Fact]
    public async Task InviteAsync_NonMember_ReturnsNotFoundAndSendsNothing()
    {
        var outsider = _fx.HasUser("outsider@example.com", "Outsider");

        var result = await Service.InviteAsync(
            outsider.Id, _organization.Id, new InviteMemberRequest(StaffEmail, OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Empty(_fx.SentInvitations);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.None)]
    public async Task InviteAsync_RoleThatIsNotAdminOrMember_IsRejected(OrganizationRole role)
    {
        var result = await Service.InviteAsync(_owner.Id, _organization.Id, new InviteMemberRequest(StaffEmail, role));

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Empty(_fx.SentInvitations);
    }

    [Theory]
    [InlineData("member@example.com")]
    [InlineData("owner@example.com")]
    public async Task InviteAsync_ExistingMember_ReturnsAlreadyMemberAndSendsNothing(string email)
    {
        var result = await Service.InviteAsync(
            _admin.Id, _organization.Id, new InviteMemberRequest(email, OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
        Assert.Empty(_fx.SentInvitations);
        Assert.Empty(_fx.PendingInvitations);
    }

    [Fact]
    public async Task InviteAsync_SameEmailAgain_ReplacesLinkAndKeepsOneInvitation()
    {
        var firstToken = await InviteStaffAsync(OrganizationRole.Member);

        var secondToken = await InviteStaffAsync(OrganizationRole.Admin);

        var invitation = Assert.Single(_fx.PendingInvitations);
        Assert.Equal(OrganizationRole.Admin, invitation.Role);
        Assert.Equal(2, _fx.SentInvitations.Count);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(OrganizationErrors.InvalidInvitation, (await Service.PreviewAsync(firstToken)).Error);
        Assert.True((await Service.PreviewAsync(secondToken)).IsSuccess);
    }

    [Fact]
    public async Task InviteAsync_SameEmailInvitedConcurrently_ReturnsConflictAndSendsNothing()
    {
        _fx.SaveHitsUniqueConstraint();

        var result = await Service.InviteAsync(
            _owner.Id, _organization.Id, new InviteMemberRequest(StaffEmail, OrganizationRole.Member));

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(1, _fx.DiscardCount);
        Assert.Empty(_fx.SentInvitations);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    public async Task ListAsync_OwnerOrAdmin_SeesPendingInvitations(OrganizationRole actorRole)
    {
        await InviteStaffAsync(OrganizationRole.Admin);

        var result = await Service.ListAsync(ActorWithRole(actorRole).Id, _organization.Id);

        var invitation = Assert.Single(result.Value);
        Assert.Equal(StaffEmail, invitation.Email);
        Assert.Equal(OrganizationRole.Admin, invitation.Role);
        Assert.Equal("Owner", invitation.InvitedByName);
    }

    [Fact]
    public async Task ListAsync_Member_ReturnsForbidden()
    {
        await InviteStaffAsync();

        var result = await Service.ListAsync(_member.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task RevokeAsync_Admin_RemovesInvitationAndLinkStopsWorking()
    {
        var rawToken = await InviteStaffAsync();
        var invitationId = _fx.PendingInvitations.Single().Id;

        var result = await Service.RevokeAsync(_admin.Id, _organization.Id, invitationId);

        Assert.True(result.IsSuccess);
        Assert.Empty(_fx.PendingInvitations);
        Assert.Equal(OrganizationErrors.InvalidInvitation, (await Service.PreviewAsync(rawToken)).Error);
    }

    [Fact]
    public async Task RevokeAsync_UnknownInvitation_ReturnsNotFound()
    {
        var result = await Service.RevokeAsync(_owner.Id, _organization.Id, Guid.NewGuid());

        Assert.Equal(OrganizationErrors.InvitationNotFound, result.Error);
    }

    [Fact]
    public async Task RevokeAsync_Member_ReturnsForbiddenAndKeepsInvitation()
    {
        await InviteStaffAsync();

        var result = await Service.RevokeAsync(_member.Id, _organization.Id, _fx.PendingInvitations.Single().Id);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Single(_fx.PendingInvitations);
    }

    [Fact]
    public async Task RevokeAsync_InvitationOfAnotherBusiness_ReturnsNotFoundAndKeepsIt()
    {
        var otherOwner = TestData.VerifiedUser("other-owner@example.com", "Other Owner");
        var other = _fx.HasOrganization(otherOwner, "Other Store", "other-store");
        await Service.InviteAsync(otherOwner.Id, other.Id, new InviteMemberRequest(StaffEmail, OrganizationRole.Member));

        var result = await Service.RevokeAsync(_owner.Id, _organization.Id, _fx.PendingInvitations.Single().Id);

        Assert.Equal(OrganizationErrors.InvitationNotFound, result.Error);
        Assert.Single(_fx.PendingInvitations);
    }

    [Fact]
    public async Task PreviewAsync_ValidLink_DescribesInvitation()
    {
        var rawToken = await InviteStaffAsync(OrganizationRole.Admin);

        var result = await Service.PreviewAsync(rawToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(_organization.Name, result.Value.OrganizationName);
        Assert.Equal(StaffEmail, result.Value.Email);
        Assert.Equal(OrganizationRole.Admin, result.Value.Role);
        Assert.Equal("Owner", result.Value.InvitedByName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task PreviewAsync_UnknownLink_ReturnsInvalidInvitation(string rawToken)
    {
        await InviteStaffAsync();

        var result = await Service.PreviewAsync(rawToken);

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
    }

    [Fact]
    public async Task PreviewAsync_DeactivatedBusiness_ReturnsInvalidInvitation()
    {
        var rawToken = await InviteStaffAsync();
        _organization.Deactivate();

        var result = await Service.PreviewAsync(rawToken);

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
    }

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin)]
    public async Task AcceptAsync_InvitedUser_JoinsWithInvitedRole(OrganizationRole role)
    {
        var rawToken = await InviteStaffAsync(role);
        var staff = _fx.HasUser(StaffEmail);

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(_organization.Id, result.Value.Id);
        Assert.Equal(role, result.Value.Role);
        var membership = Assert.Single(_fx.AddedMemberships);
        Assert.Equal(staff.Id, membership.UserId);
        Assert.Equal(_organization.Id, membership.OrganizationId);
        Assert.Equal(role, membership.Role);
        Assert.Empty(_fx.PendingInvitations);
    }

    [Fact]
    public async Task AcceptAsync_AccountCreatedAfterInvitation_CanStillAccept()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(TestData.VerifiedUser(StaffEmail, "New Staff"));

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.True(result.IsSuccess);
        Assert.Single(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AcceptAsync_SignedInAsDifferentEmail_IsForbiddenAndInvitationStays()
    {
        var rawToken = await InviteStaffAsync();
        var someoneElse = _fx.HasUser("someone-else@example.com");

        var result = await Service.AcceptAsync(someoneElse.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.InvitationForAnotherEmail, result.Error);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Single(_fx.PendingInvitations);
    }

    [Fact]
    public async Task AcceptAsync_ExpiredLink_ReturnsInvalidInvitationAndAddsNothing()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(StaffEmail);
        _fx.Clock.Advance(InvitationService.Validity + TimeSpan.FromMinutes(1));

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AcceptAsync_JustBeforeExpiry_StillWorks()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(StaffEmail);
        _fx.Clock.Advance(InvitationService.Validity - TimeSpan.FromMinutes(1));

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AcceptAsync_SameLinkTwice_SecondAttemptIsInvalid()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(StaffEmail);
        await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        var second = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.InvalidInvitation, second.Error);
        Assert.Single(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AcceptAsync_RevokedInvitation_ReturnsInvalidInvitation()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(StaffEmail);
        await Service.RevokeAsync(_owner.Id, _organization.Id, _fx.PendingInvitations.Single().Id);

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AcceptAsync_AlreadyMember_KeepsExistingRoleAndClearsInvitation()
    {
        var rawToken = await InviteStaffAsync(OrganizationRole.Member);
        var staff = _fx.HasUser(StaffEmail);
        _fx.HasMember(_organization, staff, OrganizationRole.Admin);

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationRole.Admin, result.Value.Role);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Empty(_fx.PendingInvitations);
    }

    [Fact]
    public async Task AcceptAsync_AcceptedConcurrently_ReturnsAlreadyMemberAndDiscardsChanges()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(StaffEmail);
        _fx.SaveHitsUniqueConstraint();

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
        Assert.Equal(1, _fx.DiscardCount);
    }

    [Fact]
    public async Task AcceptAsync_DeactivatedUser_ReturnsUserNotFound()
    {
        var rawToken = await InviteStaffAsync();
        var staff = _fx.HasUser(TestData.InactiveUser(StaffEmail, "Gone"));

        var result = await Service.AcceptAsync(staff.Id, new AcceptInvitationRequest(rawToken));

        Assert.Equal(OrganizationErrors.UserNotFound, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }
}
