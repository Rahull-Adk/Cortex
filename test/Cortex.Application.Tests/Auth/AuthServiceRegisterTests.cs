using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Tests.Support;
using Moq;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceRegisterTests
{
    private readonly AuthServiceFixture _fx = new();

    private static readonly RegisterRequest ValidRequest = new("Aye Chan", "aye@example.com", "S3cret-pass");

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsUnverifiedUser()
    {
        var result = await _fx.Service.RegisterAsync(ValidRequest);

        Assert.True(result.IsSuccess);
        Assert.Equal("Aye Chan", result.Value.Name);
        Assert.Equal("aye@example.com", result.Value.Email);
        Assert.False(result.Value.EmailVerified);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_SavesUserWithCredentialsAccountAndVerificationToken()
    {
        var result = await _fx.Service.RegisterAsync(ValidRequest);

        var user = Assert.Single(_fx.AddedUsers);
        var account = Assert.Single(_fx.AddedAccounts);
        var token = Assert.Single(_fx.AddedVerificationTokens);
        Assert.Equal(result.Value.Id, user.Id);
        Assert.Equal(user.Id, account.UserId);
        Assert.Equal("credentials", account.Provider);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_StoresHashedPasswordNotRawPassword()
    {
        await _fx.Service.RegisterAsync(ValidRequest);

        var account = Assert.Single(_fx.AddedAccounts);
        Assert.Equal(AuthServiceFixture.HashOf("S3cret-pass"), account.PasswordHash);
        Assert.NotEqual("S3cret-pass", account.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_SendsVerificationEmailAfterSaving()
    {
        await _fx.Service.RegisterAsync(ValidRequest);

        var email = Assert.Single(_fx.SentEmails);
        Assert.Equal("aye@example.com", email.Email);
        Assert.Equal("Aye Chan", email.Name);
        Assert.False(string.IsNullOrWhiteSpace(email.RawToken));
        Assert.Equal(1, email.SavesBeforeSend);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_StoresOnlyHashOfEmailedToken()
    {
        await _fx.Service.RegisterAsync(ValidRequest);

        var email = Assert.Single(_fx.SentEmails);
        var token = Assert.Single(_fx.AddedVerificationTokens);
        Assert.Equal(TestData.Sha256(email.RawToken), token.TokenHash);
        Assert.NotEqual(email.RawToken, token.TokenHash);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_VerificationTokenExpiresIn24Hours()
    {
        var before = DateTime.UtcNow;

        await _fx.Service.RegisterAsync(ValidRequest);

        var after = DateTime.UtcNow;
        var token = Assert.Single(_fx.AddedVerificationTokens);
        Assert.InRange(token.ExpiresAt, before.AddHours(24), after.AddHours(24));
        Assert.False(token.IsExpired);
    }

    [Fact]
    public async Task RegisterAsync_EmailWithCaseAndSpaces_StoresNormalizedEmail()
    {
        var request = ValidRequest with { Email = "  Aye.Chan@Example.COM  " };

        var result = await _fx.Service.RegisterAsync(request);

        Assert.Equal("aye.chan@example.com", result.Value.Email);
        Assert.Equal("aye.chan@example.com", Assert.Single(_fx.AddedUsers).Email);
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyRegistered_ReturnsConflictAndSavesNothing()
    {
        _fx.HasUser(TestData.VerifiedUser("aye@example.com"));

        var result = await _fx.Service.RegisterAsync(ValidRequest);

        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(_fx.AddedUsers);
        Assert.Empty(_fx.AddedAccounts);
        Assert.Empty(_fx.AddedVerificationTokens);
        Assert.Empty(_fx.SentEmails);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmailInDifferentCase_ReturnsConflict()
    {
        _fx.HasUser(TestData.VerifiedUser("aye@example.com"));
        var request = ValidRequest with { Email = " AYE@Example.com " };

        var result = await _fx.Service.RegisterAsync(request);

        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(_fx.AddedUsers);
    }

    [Fact]
    public async Task RegisterAsync_TwoRegistrations_UseDifferentVerificationTokens()
    {
        await _fx.Service.RegisterAsync(ValidRequest);
        await _fx.Service.RegisterAsync(ValidRequest with { Email = "other@example.com" });

        Assert.Equal(2, _fx.SentEmails.Count);
        Assert.NotEqual(_fx.SentEmails[0].RawToken, _fx.SentEmails[1].RawToken);
    }
}
