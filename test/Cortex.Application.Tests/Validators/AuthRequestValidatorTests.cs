using Cortex.Application.Auth.Dtos;
using Cortex.Application.Auth.Validators;
using FluentValidation.TestHelper;

namespace Cortex.Application.Tests.Validators;

public class AuthRequestValidatorTests
{
    private static readonly RegisterRequest ValidRegister = new("Aye Chan", "aye@example.com", "S3cret-pass");

    private static string EmailOfLength(int length) => new string('a', length - "@example.com".Length) + "@example.com";

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        var result = new RegisterRequestValidator().TestValidate(ValidRegister);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_BlankName_Fails(string name)
    {
        var result = new RegisterRequestValidator().TestValidate(ValidRegister with { Name = name });

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Register_NameLength_AllowsUpTo100(int length, bool valid)
    {
        var result = new RegisterRequestValidator().TestValidate(ValidRegister with { Name = new string('n', length) });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("aye@")]
    public void Register_MalformedEmail_Fails(string email)
    {
        var result = new RegisterRequestValidator().TestValidate(ValidRegister with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void Register_EmailLength_AllowsUpTo254(int length, bool valid)
    {
        var result = new RegisterRequestValidator().TestValidate(ValidRegister with { Email = EmailOfLength(length) });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Register_PasswordLength_MustBeBetween8And128(int length, bool valid)
    {
        var request = ValidRegister with { Password = new string('p', length) };

        var result = new RegisterRequestValidator().TestValidate(request);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
            result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Login_ValidRequest_Passes()
    {
        var result = new LoginRequestValidator().TestValidate(new LoginRequest("aye@example.com", "S3cret-pass"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Login_MalformedEmail_Fails(string email)
    {
        var result = new LoginRequestValidator().TestValidate(new LoginRequest(email, "S3cret-pass"));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Login_EmailLongerThan254_Fails()
    {
        var result = new LoginRequestValidator().TestValidate(new LoginRequest(EmailOfLength(255), "S3cret-pass"));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Login_PasswordLength_RequiresOneTo128WithoutMinimumOf8(int length, bool valid)
    {
        var request = new LoginRequest("aye@example.com", new string('p', length));

        var result = new LoginRequestValidator().TestValidate(request);

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("google-id-token", true)]
    public void GoogleSession_IdToken_IsRequired(string idToken, bool valid)
    {
        var result = new GoogleSessionRequestValidator().TestValidate(new GoogleSessionRequest(idToken));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("raw-token", true)]
    public void VerifyEmail_Token_IsRequired(string token, bool valid)
    {
        var result = new VerifyEmailRequestValidator().TestValidate(new VerifyEmailRequest(token));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("not-an-email", false)]
    [InlineData("aye@example.com", true)]
    public void ResendVerification_Email_MustBeAnEmailAddress(string email, bool valid)
    {
        var result = new ResendVerificationRequestValidator().TestValidate(new ResendVerificationRequest(email));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void ResendVerification_EmailLongerThan254_Fails()
    {
        var request = new ResendVerificationRequest(EmailOfLength(255));

        var result = new ResendVerificationRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
