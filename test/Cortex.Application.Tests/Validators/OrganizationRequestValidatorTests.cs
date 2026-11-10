using Cortex.Application.Organizations.Dtos;
using Cortex.Application.Organizations.Validators;
using Cortex.Domain.Enums;
using FluentValidation.TestHelper;

namespace Cortex.Application.Tests.Validators;

public class OrganizationRequestValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("shwe-store")]
    public void Create_ValidNameWithOptionalSlug_Passes(string? slug)
    {
        var result = new CreateOrganizationRequestValidator()
            .TestValidate(new CreateOrganizationRequest("Shwe Store", slug));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Fails(string name)
    {
        var result = new CreateOrganizationRequestValidator().TestValidate(new CreateOrganizationRequest(name, null));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Create_NameLength_AllowsUpTo255(int length, bool valid)
    {
        var request = new CreateOrganizationRequest(new string('n', length), null);

        var result = new CreateOrganizationRequestValidator().TestValidate(request);

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Create_SlugLength_MustBeBetween3And100(int length, bool valid)
    {
        var request = new CreateOrganizationRequest("Shwe Store", new string('a', length));

        var result = new CreateOrganizationRequestValidator().TestValidate(request);

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("shop-123")]
    [InlineData("a-b-c")]
    [InlineData("123")]
    public void Create_WellFormedSlug_Passes(string slug)
    {
        var result = new CreateOrganizationRequestValidator()
            .TestValidate(new CreateOrganizationRequest("Shwe Store", slug));

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Shwe-Store")]
    [InlineData("shwe store")]
    [InlineData("-shwe")]
    [InlineData("shwe-")]
    [InlineData("shwe--store")]
    [InlineData("shwe_store")]
    [InlineData("shwe/store")]
    [InlineData("ရွှေဆိုင်")]
    public void Create_MalformedSlug_Fails(string slug)
    {
        var result = new CreateOrganizationRequestValidator()
            .TestValidate(new CreateOrganizationRequest("Shwe Store", slug));

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://cdn.example.com/logo.png")]
    [InlineData("http://cdn.example.com/logo.png")]
    public void Update_ValidNameWithOptionalHttpLogo_Passes(string? logoUrl)
    {
        var result = new UpdateOrganizationRequestValidator()
            .TestValidate(new UpdateOrganizationRequest("Shwe Store", logoUrl));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("logo.png")]
    [InlineData("/images/logo.png")]
    [InlineData("ftp://cdn.example.com/logo.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Update_LogoThatIsNotAnHttpUrl_Fails(string logoUrl)
    {
        var result = new UpdateOrganizationRequestValidator()
            .TestValidate(new UpdateOrganizationRequest("Shwe Store", logoUrl));

        result.ShouldHaveValidationErrorFor(x => x.LogoUrl);
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    public void Update_LogoUrlLength_AllowsUpTo2048(int length, bool valid)
    {
        const string prefix = "https://cdn.example.com/";
        var logoUrl = prefix + new string('a', length - prefix.Length);

        var result = new UpdateOrganizationRequestValidator()
            .TestValidate(new UpdateOrganizationRequest("Shwe Store", logoUrl));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void Update_Name_IsRequiredAndAtMost255(object nameOrLength, bool valid)
    {
        var name = nameOrLength is int length ? new string('n', length) : (string)nameOrLength;

        var result = new UpdateOrganizationRequestValidator().TestValidate(new UpdateOrganizationRequest(name, null));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(OrganizationRole.Admin, true)]
    [InlineData(OrganizationRole.Member, true)]
    [InlineData(OrganizationRole.Owner, false)]
    [InlineData(OrganizationRole.None, false)]
    [InlineData((OrganizationRole)99, false)]
    public void AddMember_Role_MustBeAdminOrMember(OrganizationRole role, bool valid)
    {
        var result = new AddMemberRequestValidator().TestValidate(new AddMemberRequest("staff@example.com", role));

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void AddMember_MalformedEmail_Fails(string email)
    {
        var result = new AddMemberRequestValidator()
            .TestValidate(new AddMemberRequest(email, OrganizationRole.Member));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(OrganizationRole.Admin, true)]
    [InlineData(OrganizationRole.Member, true)]
    [InlineData(OrganizationRole.Owner, false)]
    [InlineData(OrganizationRole.None, false)]
    [InlineData((OrganizationRole)99, false)]
    public void ChangeMemberRole_Role_MustBeAdminOrMember(OrganizationRole role, bool valid)
    {
        var result = new ChangeMemberRoleRequestValidator().TestValidate(new ChangeMemberRoleRequest(role));

        Assert.Equal(valid, result.IsValid);
    }
}
