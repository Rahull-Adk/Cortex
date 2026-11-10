using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Domain;

public class OrganizationTests
{
    [Fact]
    public void Create_ValidInput_MakesCreatorTheOnlyMemberAsOwner()
    {
        var ownerId = Guid.NewGuid();

        var organization = Organization.Create("Shwe Store", "shwe-store", ownerId);

        var membership = Assert.Single(organization.Memberships);
        Assert.Equal(ownerId, membership.UserId);
        Assert.Equal(organization.Id, membership.OrganizationId);
        Assert.Equal(OrganizationRole.Owner, membership.Role);
    }

    [Fact]
    public void Create_ValidInput_IsActiveWithTrimmedNameAndLowercaseSlug()
    {
        var organization = Organization.Create("  Shwe Store ", "  Shwe-Store ", Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal("Shwe Store", organization.Name);
        Assert.Equal("shwe-store", organization.Slug);
        Assert.True(organization.IsActive);
        Assert.Null(organization.LogoUrl);
    }

    [Fact]
    public void Create_WithoutOwner_Throws()
    {
        Assert.Throws<ArgumentException>(() => Organization.Create("Shwe Store", "shwe-store", Guid.Empty));
    }

    [Theory]
    [InlineData("", "shwe-store")]
    [InlineData("   ", "shwe-store")]
    [InlineData("Shwe Store", "")]
    [InlineData("Shwe Store", "   ")]
    public void Create_BlankNameOrSlug_Throws(string name, string slug)
    {
        Assert.ThrowsAny<ArgumentException>(() => Organization.Create(name, slug, Guid.NewGuid()));
    }

    [Fact]
    public void Create_TwoBusinessesForSameOwner_HaveSeparateIdsAndMemberships()
    {
        var ownerId = Guid.NewGuid();

        var first = Organization.Create("Shwe Store", "shwe-store", ownerId);
        var second = Organization.Create("Ngwe Store", "ngwe-store", ownerId);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Id, Assert.Single(first.Memberships).OrganizationId);
        Assert.Equal(second.Id, Assert.Single(second.Memberships).OrganizationId);
    }

    [Fact]
    public void UpdateDetails_ValidInput_ChangesNameAndLogoButNotSlugOrOwner()
    {
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("Shwe Store", "shwe-store", ownerId);

        organization.UpdateDetails("  Shwe Store Mandalay ", "https://cdn.example.com/logo.png");

        Assert.Equal("Shwe Store Mandalay", organization.Name);
        Assert.Equal("https://cdn.example.com/logo.png", organization.LogoUrl);
        Assert.Equal("shwe-store", organization.Slug);
        Assert.Equal(ownerId, Assert.Single(organization.Memberships).UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_BlankName_ThrowsAndKeepsName(string name)
    {
        var organization = Organization.Create("Shwe Store", "shwe-store", Guid.NewGuid());

        Assert.ThrowsAny<ArgumentException>(() => organization.UpdateDetails(name, null));

        Assert.Equal("Shwe Store", organization.Name);
    }

    [Fact]
    public void Deactivate_ActiveBusiness_BecomesInactive()
    {
        var organization = Organization.Create("Shwe Store", "shwe-store", Guid.NewGuid());

        organization.Deactivate();

        Assert.False(organization.IsActive);
    }

    [Fact]
    public void MembershipCreate_ValidInput_HoldsGivenRole()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var membership = OrganizationMembership.Create(organizationId, userId, OrganizationRole.Member);

        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(OrganizationRole.Member, membership.Role);
    }

    [Fact]
    public void MembershipChangeRole_NewRole_ReplacesRole()
    {
        var membership = OrganizationMembership.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationRole.Member);

        membership.ChangeRole(OrganizationRole.Admin);

        Assert.Equal(OrganizationRole.Admin, membership.Role);
    }
}
