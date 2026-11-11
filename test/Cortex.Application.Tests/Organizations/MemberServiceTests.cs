using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Organizations;

public class MemberServiceTests
{
    private readonly OrganizationFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");
    private readonly User _admin = TestData.VerifiedUser("admin@example.com", "Admin");
    private readonly User _member = TestData.VerifiedUser("member@example.com", "Member");
    private readonly Organization _organization;
    private readonly OrganizationMembership _adminMembership;
    private readonly OrganizationMembership _memberMembership;

    public MemberServiceTests()
    {
        _organization = _fx.HasOrganization(_owner);
        _adminMembership = _fx.HasMember(_organization, _admin, OrganizationRole.Admin);
        _memberMembership = _fx.HasMember(_organization, _member, OrganizationRole.Member);
    }

    private MemberService Service => _fx.MemberService;

    private User ActorWithRole(OrganizationRole role) => role switch
    {
        OrganizationRole.Owner => _owner,
        OrganizationRole.Admin => _admin,
        OrganizationRole.Member => _member,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    public async Task ListAsync_AnyRole_SeesEveryMemberWithTheirRole(OrganizationRole actorRole)
    {
        var result = await Service.ListAsync(ActorWithRole(actorRole).Id, _organization.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(OrganizationRole.Owner, result.Value.Single(m => m.UserId == _owner.Id).Role);
        Assert.Equal(OrganizationRole.Admin, result.Value.Single(m => m.UserId == _admin.Id).Role);
        Assert.Equal(OrganizationRole.Member, result.Value.Single(m => m.UserId == _member.Id).Role);
    }

    [Fact]
    public async Task ListAsync_Member_ReturnsUserDetailsForEachMember()
    {
        var result = await Service.ListAsync(_member.Id, _organization.Id);

        var admin = result.Value.Single(m => m.UserId == _admin.Id);
        Assert.Equal("Admin", admin.Name);
        Assert.Equal("admin@example.com", admin.Email);
        Assert.Equal(_adminMembership.JoinedAt, admin.JoinedAt);
    }

    [Fact]
    public async Task ListAsync_NonMember_ReturnsNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.ListAsync(outsider.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task ListAsync_DoesNotLeakMembersOfOtherBusinesses()
    {
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");

        var result = await Service.ListAsync(_owner.Id, _organization.Id);

        Assert.DoesNotContain(result.Value, m => m.UserId == otherOwner.Id);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Member)]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Admin)]
    public async Task AddAsync_OwnerOrAdmin_AddsRegisteredUserWithRequestedRole(
        OrganizationRole actorRole, OrganizationRole newRole)
    {
        var staff = _fx.HasUser("staff@example.com", "Staff");

        var result = await Service.AddAsync(
            ActorWithRole(actorRole).Id, _organization.Id, new AddMemberRequest("staff@example.com", newRole));

        Assert.True(result.IsSuccess);
        Assert.Equal(staff.Id, result.Value.UserId);
        Assert.Equal("Staff", result.Value.Name);
        Assert.Equal("staff@example.com", result.Value.Email);
        Assert.Equal(newRole, result.Value.Role);

        var added = Assert.Single(_fx.AddedMemberships);
        Assert.Equal(_organization.Id, added.OrganizationId);
        Assert.Equal(staff.Id, added.UserId);
        Assert.Equal(newRole, added.Role);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task AddAsync_EmailWithCaseAndSpaces_FindsUserByNormalizedEmail()
    {
        var staff = _fx.HasUser("staff@example.com");

        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("  Staff@Example.COM ", OrganizationRole.Member));

        Assert.True(result.IsSuccess);
        Assert.Equal(staff.Id, result.Value.UserId);
    }

    [Fact]
    public async Task AddAsync_Member_ReturnsForbiddenAndAddsNothing()
    {
        _fx.HasUser("staff@example.com");

        var result = await Service.AddAsync(
            _member.Id, _organization.Id, new AddMemberRequest("staff@example.com", OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task AddAsync_NonMember_ReturnsNotFoundAndAddsNothing()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.AddAsync(
            outsider.Id, _organization.Id, new AddMemberRequest("outsider@example.com", OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Owner, OrganizationRole.None)]
    [InlineData(OrganizationRole.Owner, (OrganizationRole)99)]
    public async Task AddAsync_RoleThatIsNotAdminOrMember_IsRejectedAndAddsNothing(
        OrganizationRole actorRole, OrganizationRole newRole)
    {
        _fx.HasUser("staff@example.com");

        var result = await Service.AddAsync(
            ActorWithRole(actorRole).Id, _organization.Id, new AddMemberRequest("staff@example.com", newRole));

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task AddAsync_EmailNotRegistered_ReturnsUserNotRegistered()
    {
        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("nobody@example.com", OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.UserNotRegistered, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AddAsync_DeactivatedUser_ReturnsUserNotRegistered()
    {
        _fx.HasUser(TestData.InactiveUser("gone@example.com"));

        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("gone@example.com", OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.UserNotRegistered, result.Error);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AddAsync_ExistingMember_ReturnsAlreadyMemberAndKeepsTheirRole()
    {
        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("member@example.com", OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
        Assert.Equal(OrganizationRole.Member, _memberMembership.Role);
        Assert.Empty(_fx.AddedMemberships);
    }

    [Fact]
    public async Task AddAsync_OwnerAddedAgainAsMember_ReturnsAlreadyMemberAndStaysOwner()
    {
        var result = await Service.AddAsync(
            _admin.Id, _organization.Id, new AddMemberRequest("owner@example.com", OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
        Assert.Equal(OrganizationRole.Owner, _organization.Memberships.Single().Role);
    }

    [Fact]
    public async Task AddAsync_MemberOfAnotherBusiness_CanBeAddedHere()
    {
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");

        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("other@example.com", OrganizationRole.Member));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationRole.Member, result.Value.Role);
    }

    [Fact]
    public async Task AddAsync_SameUserAddedConcurrently_ReturnsAlreadyMemberAndDiscardsChanges()
    {
        _fx.HasUser("staff@example.com");
        _fx.SaveHitsUniqueConstraint();

        var result = await Service.AddAsync(
            _owner.Id, _organization.Id, new AddMemberRequest("staff@example.com", OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.AlreadyMember, result.Error);
        Assert.Equal(1, _fx.DiscardCount);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    public async Task ChangeRoleAsync_OwnerOrAdmin_PromotesMemberToAdmin(OrganizationRole actorRole)
    {
        var result = await Service.ChangeRoleAsync(
            ActorWithRole(actorRole).Id,
            _organization.Id,
            _member.Id,
            new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.True(result.IsSuccess);
        Assert.Equal(_member.Id, result.Value.UserId);
        Assert.Equal(OrganizationRole.Admin, result.Value.Role);
        Assert.Equal(OrganizationRole.Admin, _memberMembership.Role);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task ChangeRoleAsync_Owner_DemotesAdminToMember()
    {
        var result = await Service.ChangeRoleAsync(
            _owner.Id, _organization.Id, _admin.Id, new ChangeMemberRoleRequest(OrganizationRole.Member));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationRole.Member, _adminMembership.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_DemotedAdmin_LosesMemberManagementImmediately()
    {
        await Service.ChangeRoleAsync(
            _owner.Id, _organization.Id, _admin.Id, new ChangeMemberRoleRequest(OrganizationRole.Member));

        var result = await Service.ChangeRoleAsync(
            _admin.Id, _organization.Id, _member.Id, new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Equal(OrganizationRole.Member, _memberMembership.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_Member_ReturnsForbiddenAndChangesNothing()
    {
        var result = await Service.ChangeRoleAsync(
            _member.Id, _organization.Id, _admin.Id, new ChangeMemberRoleRequest(OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Equal(OrganizationRole.Admin, _adminMembership.Role);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task ChangeRoleAsync_MemberPromotingThemselves_ReturnsForbidden()
    {
        var result = await Service.ChangeRoleAsync(
            _member.Id, _organization.Id, _member.Id, new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Equal(OrganizationRole.Member, _memberMembership.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_NonMemberActor_ReturnsNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.ChangeRoleAsync(
            outsider.Id, _organization.Id, _member.Id, new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Equal(OrganizationRole.Member, _memberMembership.Role);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    public async Task ChangeRoleAsync_TargetIsOwner_IsRejectedAndOwnerStaysOwner(OrganizationRole actorRole)
    {
        var result = await Service.ChangeRoleAsync(
            ActorWithRole(actorRole).Id,
            _organization.Id,
            _owner.Id,
            new ChangeMemberRoleRequest(OrganizationRole.Member));

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Equal(OrganizationRole.Owner, _organization.Memberships.Single().Role);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Owner, OrganizationRole.None)]
    [InlineData(OrganizationRole.Owner, (OrganizationRole)99)]
    public async Task ChangeRoleAsync_NewRoleThatIsNotAdminOrMember_IsRejectedAndKeepsRole(
        OrganizationRole actorRole, OrganizationRole newRole)
    {
        var result = await Service.ChangeRoleAsync(
            ActorWithRole(actorRole).Id, _organization.Id, _member.Id, new ChangeMemberRoleRequest(newRole));

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Equal(OrganizationRole.Member, _memberMembership.Role);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task ChangeRoleAsync_AdminMakingThemselvesOwner_IsRejected()
    {
        var result = await Service.ChangeRoleAsync(
            _admin.Id, _organization.Id, _admin.Id, new ChangeMemberRoleRequest(OrganizationRole.Owner));

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Equal(OrganizationRole.Admin, _adminMembership.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_TargetNotInBusiness_ReturnsMemberNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.ChangeRoleAsync(
            _owner.Id, _organization.Id, outsider.Id, new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.MemberNotFound, result.Error);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task ChangeRoleAsync_TargetOnlyInAnotherBusiness_ReturnsMemberNotFoundAndLeavesThemAlone()
    {
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        var other = _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");
        var otherStaff = TestData.VerifiedUser("otherstaff@example.com", "Other Staff");
        var otherMembership = _fx.HasMember(other, otherStaff, OrganizationRole.Member);

        var result = await Service.ChangeRoleAsync(
            _owner.Id, _organization.Id, otherStaff.Id, new ChangeMemberRoleRequest(OrganizationRole.Admin));

        Assert.Equal(OrganizationErrors.MemberNotFound, result.Error);
        Assert.Equal(OrganizationRole.Member, otherMembership.Role);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.Admin)]
    public async Task RemoveAsync_OwnerOrAdmin_RemovesMember(OrganizationRole actorRole)
    {
        var result = await Service.RemoveAsync(ActorWithRole(actorRole).Id, _organization.Id, _member.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(_memberMembership, Assert.Single(_fx.RemovedMemberships));
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_Owner_RemovesAdmin()
    {
        var result = await Service.RemoveAsync(_owner.Id, _organization.Id, _admin.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(_adminMembership, Assert.Single(_fx.RemovedMemberships));
    }

    [Fact]
    public async Task RemoveAsync_RemovedMember_LosesAccessToTheBusiness()
    {
        await Service.RemoveAsync(_owner.Id, _organization.Id, _member.Id);

        var result = await Service.ListAsync(_member.Id, _organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    public async Task RemoveAsync_NonOwnerRemovingThemselves_CanLeave(OrganizationRole actorRole)
    {
        var actor = ActorWithRole(actorRole);

        var result = await Service.RemoveAsync(actor.Id, _organization.Id, actor.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(actor.Id, Assert.Single(_fx.RemovedMemberships).UserId);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_OwnerLeaving_IsRejectedSoBusinessKeepsItsOwner()
    {
        var result = await Service.RemoveAsync(_owner.Id, _organization.Id, _owner.Id);

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_AdminRemovingOwner_IsRejected()
    {
        var result = await Service.RemoveAsync(_admin.Id, _organization.Id, _owner.Id);

        Assert.Equal(OrganizationErrors.OwnerImmutable, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
    }

    [Theory]
    [InlineData("admin@example.com")]
    [InlineData("owner@example.com")]
    public async Task RemoveAsync_MemberRemovingSomeoneElse_ReturnsForbidden(string targetEmail)
    {
        var target = targetEmail == _admin.Email ? _admin : _owner;

        var result = await Service.RemoveAsync(_member.Id, _organization.Id, target.Id);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_NonMemberActor_ReturnsNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.RemoveAsync(outsider.Id, _organization.Id, _member.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
    }

    [Fact]
    public async Task RemoveAsync_NonMemberRemovingThemselves_ReturnsNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.RemoveAsync(outsider.Id, _organization.Id, outsider.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task RemoveAsync_TargetNotInBusiness_ReturnsMemberNotFound()
    {
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.RemoveAsync(_owner.Id, _organization.Id, outsider.Id);

        Assert.Equal(OrganizationErrors.MemberNotFound, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_AdminOfAnotherBusiness_CannotRemoveMembersHere()
    {
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");

        var result = await Service.RemoveAsync(otherOwner.Id, _organization.Id, _member.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Empty(_fx.RemovedMemberships);
    }
}
