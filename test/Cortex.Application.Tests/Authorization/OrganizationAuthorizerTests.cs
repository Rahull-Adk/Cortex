using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Moq;

namespace Cortex.Application.Tests.Authorization;

public class OrganizationAuthorizerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Mock<IOrganizationMembershipRepository> _memberships = new();
    private readonly OrganizationAuthorizer _authorizer;

    public OrganizationAuthorizerTests() => _authorizer = new OrganizationAuthorizer(_memberships.Object);

    private OrganizationMembership HasMembership(Guid organizationId, Guid userId, OrganizationRole role)
    {
        var membership = OrganizationMembership.Create(organizationId, userId, role);
        _memberships.Setup(r => r.FindActiveAsync(organizationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        return membership;
    }

    [Fact]
    public async Task AuthorizeAsync_NoMembership_ReturnsNotFoundRatherThanForbidden()
    {
        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task AuthorizeAsync_MemberWithPermission_ReturnsTheirMembership()
    {
        var membership = HasMembership(_organizationId, _userId, OrganizationRole.Member);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.True(result.IsSuccess);
        Assert.Same(membership, result.Value);
    }

    [Fact]
    public async Task AuthorizeAsync_MemberWithoutPermission_ReturnsForbidden()
    {
        HasMembership(_organizationId, _userId, OrganizationRole.Member);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationUpdate);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, Permission.BillingManage, true)]
    [InlineData(OrganizationRole.Owner, Permission.OrganizationDelete, true)]
    [InlineData(OrganizationRole.Admin, Permission.MembersManage, true)]
    [InlineData(OrganizationRole.Admin, Permission.OrganizationDelete, false)]
    [InlineData(OrganizationRole.Admin, Permission.OwnershipTransfer, false)]
    [InlineData(OrganizationRole.Admin, Permission.BillingManage, false)]
    [InlineData(OrganizationRole.Member, Permission.MembersView, true)]
    [InlineData(OrganizationRole.Member, Permission.MembersManage, false)]
    [InlineData(OrganizationRole.Member, Permission.BillingView, false)]
    public async Task AuthorizeAsync_RoleAndPermission_AllowsOnlyWhatTheRoleHolds(
        OrganizationRole role, Permission permission, bool allowed)
    {
        HasMembership(_organizationId, _userId, role);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, permission);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
            Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_MembershipWithNoRole_ReturnsForbidden()
    {
        HasMembership(_organizationId, _userId, OrganizationRole.None);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_OwnerOfAnotherBusiness_GetsNotFoundForThisOne()
    {
        var otherOrganizationId = Guid.NewGuid();
        HasMembership(otherOrganizationId, _userId, OrganizationRole.Owner);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_AnotherUsersMembership_DoesNotAuthorizeCaller()
    {
        HasMembership(_organizationId, Guid.NewGuid(), OrganizationRole.Owner);

        var result = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task AuthorizeAsync_RoleChangedBetweenRequests_UsesCurrentRole()
    {
        var membership = HasMembership(_organizationId, _userId, OrganizationRole.Member);

        var before = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.MembersManage);
        membership.ChangeRole(OrganizationRole.Admin);
        var after = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.MembersManage);

        Assert.Equal(OrganizationErrors.Forbidden, before.Error);
        Assert.True(after.IsSuccess);
    }

    [Fact]
    public async Task AuthorizeAsync_MembershipRemovedBetweenRequests_LosesAccess()
    {
        HasMembership(_organizationId, _userId, OrganizationRole.Admin);

        var before = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);
        _memberships.Setup(r => r.FindActiveAsync(_organizationId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrganizationMembership?)null);
        var after = await _authorizer.AuthorizeAsync(_userId, _organizationId, Permission.OrganizationView);

        Assert.True(before.IsSuccess);
        Assert.Equal(OrganizationErrors.NotFound, after.Error);
    }
}
