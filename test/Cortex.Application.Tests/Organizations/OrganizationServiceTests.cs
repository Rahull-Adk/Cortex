using Cortex.Application.Organizations;
using Cortex.Application.Organizations.Dtos;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Organizations;

public class OrganizationServiceTests
{
    private readonly OrganizationFixture _fx = new();
    private readonly User _owner = TestData.VerifiedUser("owner@example.com", "Owner");

    private OrganizationService Service => _fx.OrganizationService;

    [Fact]
    public async Task CreateAsync_ExplicitSlug_ReturnsBusinessWithCallerAsOwner()
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "shwe-store"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Shwe Store", result.Value.Name);
        Assert.Equal("shwe-store", result.Value.Slug);
        Assert.Equal(OrganizationRole.Owner, result.Value.Role);
        Assert.Null(result.Value.LogoUrl);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesBusinessWithOwnerMembershipOnce()
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "shwe-store"));

        var organization = Assert.Single(_fx.AddedOrganizations);
        var membership = Assert.Single(organization.Memberships);
        Assert.Equal(result.Value.Id, organization.Id);
        Assert.Equal(_owner.Id, membership.UserId);
        Assert.Equal(OrganizationRole.Owner, membership.Role);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ExplicitSlugWithCaseAndSpaces_ChecksAndStoresNormalizedSlug()
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "  Shwe-Store "));

        Assert.Equal("shwe-store", result.Value.Slug);
        Assert.Equal(["shwe-store"], _fx.CheckedSlugs);
    }

    [Fact]
    public async Task CreateAsync_ExplicitSlugTaken_ReturnsConflictAndSavesNothing()
    {
        _fx.HasUser(_owner);
        _fx.HasTakenSlug("shwe-store");

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "shwe-store"));

        Assert.Equal(OrganizationErrors.SlugTaken, result.Error);
        Assert.Empty(_fx.AddedOrganizations);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ExplicitSlugTakenInDifferentCase_ReturnsConflict()
    {
        _fx.HasUser(_owner);
        _fx.HasTakenSlug("shwe-store");

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "SHWE-Store"));

        Assert.Equal(OrganizationErrors.SlugTaken, result.Error);
    }

    [Theory]
    [InlineData("Shwe Store", "shwe-store")]
    [InlineData("  Shwe   Store  ", "shwe-store")]
    [InlineData("Shwe Store & Co.", "shwe-store-co")]
    [InlineData("Café 21!", "caf-21")]
    [InlineData("--Shwe--", "shwe")]
    public async Task CreateAsync_NoSlug_DerivesSlugFromName(string name, string expectedSlug)
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest(name, null));

        Assert.Equal(expectedSlug, result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_DerivedSlugTaken_AppendsRandomSuffixInsteadOfFailing()
    {
        _fx.HasUser(_owner);
        _fx.HasTakenSlug("shwe-store");

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", null));

        Assert.True(result.IsSuccess);
        Assert.Matches("^shwe-store-[0-9a-f]{6}$", result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_DerivedSlugTakenTwice_UsesDifferentSuffixes()
    {
        _fx.HasUser(_owner);
        _fx.HasTakenSlug("shwe-store");

        var first = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", null));
        var second = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", null));

        Assert.NotEqual(first.Value.Slug, second.Value.Slug);
    }

    [Theory]
    [InlineData("ရွှေဆိုင်")]
    [InlineData("!!!")]
    public async Task CreateAsync_NameWithNoLatinLettersOrDigits_UsesBusinessPrefixedSlug(string name)
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest(name, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.Value.Name);
        Assert.Matches("^business-[0-9a-f]{6}$", result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_NameTooShortForSlug_AppendsRandomSuffix()
    {
        _fx.HasUser(_owner);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("AB", null));

        Assert.Matches("^ab-[0-9a-f]{6}$", result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_VeryLongName_DerivedSlugFitsSlugLimitEvenWithSuffix()
    {
        _fx.HasUser(_owner);
        var name = new string('a', 255);
        _fx.HasTakenSlug(new string('a', 90));

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest(name, null));

        Assert.StartsWith(new string('a', 90) + "-", result.Value.Slug);
        Assert.InRange(result.Value.Slug.Length, 3, 100);
    }

    [Fact]
    public async Task CreateAsync_LongNameCutAtSeparator_DerivedSlugDoesNotEndWithHyphen()
    {
        _fx.HasUser(_owner);
        var name = new string('a', 89) + " " + new string('b', 20);

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest(name, null));

        Assert.Equal(new string('a', 89), result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_UnknownUser_ReturnsUserNotFoundAndSavesNothing()
    {
        var result = await Service.CreateAsync(Guid.NewGuid(), new CreateOrganizationRequest("Shwe Store", null));

        Assert.Equal(OrganizationErrors.UserNotFound, result.Error);
        Assert.Empty(_fx.AddedOrganizations);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_DeactivatedUser_ReturnsUserNotFound()
    {
        var inactive = _fx.HasUser(TestData.InactiveUser());

        var result = await Service.CreateAsync(inactive.Id, new CreateOrganizationRequest("Shwe Store", null));

        Assert.Equal(OrganizationErrors.UserNotFound, result.Error);
        Assert.Empty(_fx.AddedOrganizations);
    }

    [Fact]
    public async Task CreateAsync_SlugClaimedByConcurrentRequest_ReturnsConflictAndDiscardsChanges()
    {
        _fx.HasUser(_owner);
        _fx.SaveHitsUniqueConstraint();

        var result = await Service.CreateAsync(_owner.Id, new CreateOrganizationRequest("Shwe Store", "shwe-store"));

        Assert.Equal(OrganizationErrors.SlugTaken, result.Error);
        Assert.Equal(1, _fx.DiscardCount);
    }

    [Fact]
    public async Task ListMineAsync_UserInSeveralBusinesses_ReturnsEachWithTheirRoleThere()
    {
        var staff = TestData.VerifiedUser("staff@example.com", "Staff");
        var own = _fx.HasOrganization(staff, "Ngwe Store", "ngwe-store");
        var other = _fx.HasOrganization(_owner, "Shwe Store", "shwe-store");
        _fx.HasMember(other, staff, OrganizationRole.Member);

        var result = await Service.ListMineAsync(staff.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal(OrganizationRole.Owner, result.Single(o => o.Id == own.Id).Role);
        Assert.Equal(OrganizationRole.Member, result.Single(o => o.Id == other.Id).Role);
    }

    [Fact]
    public async Task ListMineAsync_DoesNotIncludeBusinessesTheUserIsNotIn()
    {
        var staff = TestData.VerifiedUser("staff@example.com", "Staff");
        var own = _fx.HasOrganization(staff, "Ngwe Store", "ngwe-store");
        _fx.HasOrganization(_owner, "Shwe Store", "shwe-store");

        var result = await Service.ListMineAsync(staff.Id);

        Assert.Equal(own.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task ListMineAsync_UserWithNoBusinesses_ReturnsEmpty()
    {
        var result = await Service.ListMineAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    public async Task GetAsync_AnyMember_ReturnsBusinessWithTheirOwnRole(OrganizationRole role)
    {
        var organization = _fx.HasOrganization(_owner);
        var staff = TestData.VerifiedUser("staff@example.com", "Staff");
        _fx.HasMember(organization, staff, role);

        var result = await Service.GetAsync(staff.Id, organization.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(organization.Id, result.Value.Id);
        Assert.Equal("Shwe Store", result.Value.Name);
        Assert.Equal("shwe-store", result.Value.Slug);
        Assert.Equal(role, result.Value.Role);
    }

    [Fact]
    public async Task GetAsync_Owner_ReturnsBusinessWithOwnerRole()
    {
        var organization = _fx.HasOrganization(_owner);

        var result = await Service.GetAsync(_owner.Id, organization.Id);

        Assert.Equal(OrganizationRole.Owner, result.Value.Role);
    }

    [Fact]
    public async Task GetAsync_NonMember_ReturnsNotFound()
    {
        var organization = _fx.HasOrganization(_owner);
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.GetAsync(outsider.Id, organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task GetAsync_OwnerOfAnotherBusiness_ReturnsNotFound()
    {
        var organization = _fx.HasOrganization(_owner);
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");

        var result = await Service.GetAsync(otherOwner.Id, organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task GetAsync_UnknownBusiness_ReturnsNotFound()
    {
        _fx.HasOrganization(_owner);

        var result = await Service.GetAsync(_owner.Id, Guid.NewGuid());

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task GetAsync_DeactivatedBusiness_ReturnsNotFoundEvenForOwner()
    {
        var organization = _fx.HasOrganization(_owner);
        organization.Deactivate();

        var result = await Service.GetAsync(_owner.Id, organization.Id);

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_Owner_ChangesNameAndLogoAndSaves()
    {
        var organization = _fx.HasOrganization(_owner);
        var request = new UpdateOrganizationRequest("Shwe Store Mandalay", "https://cdn.example.com/logo.png");

        var result = await Service.UpdateAsync(_owner.Id, organization.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Shwe Store Mandalay", result.Value.Name);
        Assert.Equal("https://cdn.example.com/logo.png", result.Value.LogoUrl);
        Assert.Equal(OrganizationRole.Owner, result.Value.Role);
        Assert.Equal("Shwe Store Mandalay", organization.Name);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_Admin_IsAllowedAndKeepsAdminRoleInResponse()
    {
        var organization = _fx.HasOrganization(_owner);
        var admin = TestData.VerifiedUser("admin@example.com", "Admin");
        _fx.HasMember(organization, admin, OrganizationRole.Admin);

        var result = await Service.UpdateAsync(
            admin.Id, organization.Id, new UpdateOrganizationRequest("Renamed", null));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationRole.Admin, result.Value.Role);
        Assert.Equal("Renamed", organization.Name);
    }

    [Fact]
    public async Task UpdateAsync_Member_ReturnsForbiddenAndChangesNothing()
    {
        var organization = _fx.HasOrganization(_owner);
        var member = TestData.VerifiedUser("member@example.com", "Member");
        _fx.HasMember(organization, member, OrganizationRole.Member);

        var result = await Service.UpdateAsync(
            member.Id, organization.Id, new UpdateOrganizationRequest("Hijacked", "https://evil.example.com/x.png"));

        Assert.Equal(OrganizationErrors.Forbidden, result.Error);
        Assert.Equal("Shwe Store", organization.Name);
        Assert.Null(organization.LogoUrl);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_NonMember_ReturnsNotFoundAndChangesNothing()
    {
        var organization = _fx.HasOrganization(_owner);
        var outsider = _fx.HasUser("outsider@example.com");

        var result = await Service.UpdateAsync(
            outsider.Id, organization.Id, new UpdateOrganizationRequest("Hijacked", null));

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Equal("Shwe Store", organization.Name);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_AdminOfAnotherBusiness_CannotUpdateThisOne()
    {
        var organization = _fx.HasOrganization(_owner);
        var otherOwner = TestData.VerifiedUser("other@example.com", "Other");
        var other = _fx.HasOrganization(otherOwner, "Ngwe Store", "ngwe-store");
        var admin = TestData.VerifiedUser("admin@example.com", "Admin");
        _fx.HasMember(other, admin, OrganizationRole.Admin);

        var result = await Service.UpdateAsync(
            admin.Id, organization.Id, new UpdateOrganizationRequest("Hijacked", null));

        Assert.Equal(OrganizationErrors.NotFound, result.Error);
        Assert.Equal("Shwe Store", organization.Name);
    }

    [Fact]
    public async Task UpdateAsync_NullLogo_ClearsExistingLogoAndKeepsSlug()
    {
        var organization = _fx.HasOrganization(_owner);
        organization.UpdateDetails("Shwe Store", "https://cdn.example.com/logo.png");

        var result = await Service.UpdateAsync(
            _owner.Id, organization.Id, new UpdateOrganizationRequest("Shwe Store", null));

        Assert.Null(result.Value.LogoUrl);
        Assert.Equal("shwe-store", result.Value.Slug);
    }
}
