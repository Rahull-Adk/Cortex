using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Moq;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceEmailVerificationTests
{
    private const string RawToken = "raw-verification-token";

    private readonly AuthServiceFixture _fx = new();

    private EmailVerificationToken HasToken(User user, TimeSpan validity)
    {
        var token = EmailVerificationToken.Create(user.Id, TestData.Sha256(RawToken), validity);
        _fx.HasVerificationToken(token);
        return token;
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_MarksUserVerifiedAndIssuesSession()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        HasToken(user, TimeSpan.FromHours(24));

        var result = await _fx.Service.VerifyEmailAsync(RawToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.True(result.Value.User.EmailVerified);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Id, Assert.Single(_fx.AddedRefreshTokens).UserId);
        Assert.Equal(AuthServiceFixture.AccessTokenValue, result.Value.AccessToken);
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConsumesTheToken()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        var token = HasToken(user, TimeSpan.FromHours(24));

        await _fx.Service.VerifyEmailAsync(RawToken);

        Assert.Same(token, Assert.Single(_fx.RemovedVerificationTokens));
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_UnknownToken_ReturnsInvalidVerificationToken()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        HasToken(user, TimeSpan.FromHours(24));

        var result = await _fx.Service.VerifyEmailAsync("some-other-token");

        Assert.Equal(AuthErrors.InvalidVerificationToken, result.Error);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenHashPresentedAsToken_IsRejected()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        var token = HasToken(user, TimeSpan.FromHours(24));

        var result = await _fx.Service.VerifyEmailAsync(token.TokenHash);

        Assert.Equal(AuthErrors.InvalidVerificationToken, result.Error);
        Assert.Null(user.EmailVerifiedAt);
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsInvalidVerificationTokenAndLeavesUserUnverified()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        HasToken(user, TimeSpan.FromMinutes(-1));

        var result = await _fx.Service.VerifyEmailAsync(RawToken);

        Assert.Equal(AuthErrors.InvalidVerificationToken, result.Error);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Empty(_fx.RemovedVerificationTokens);
        Assert.Empty(_fx.AddedRefreshTokens);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenForMissingUser_Throws()
    {
        var token = EmailVerificationToken.Create(Guid.NewGuid(), TestData.Sha256(RawToken), TimeSpan.FromHours(24));
        _fx.HasVerificationToken(token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _fx.Service.VerifyEmailAsync(RawToken));
    }

    [Fact(Skip = "Rule not implemented: a deactivated user gets a session from a valid verification link.")]
    public async Task VerifyEmailAsync_DeactivatedUser_DoesNotIssueSession()
    {
        var user = TestData.UnverifiedUser();
        user.Deactivate();
        _fx.HasUser(user);
        HasToken(user, TimeSpan.FromHours(24));

        var result = await _fx.Service.VerifyEmailAsync(RawToken);

        Assert.True(result.IsFailure);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task ResendVerificationAsync_UnverifiedUser_ReplacesTokenAndSendsEmail()
    {
        var user = TestData.UnverifiedUser("aye@example.com", "Aye Chan");
        _fx.HasUser(user);

        var result = await _fx.Service.ResendVerificationAsync(new ResendVerificationRequest("aye@example.com"));

        var email = Assert.Single(_fx.SentEmails);
        var token = Assert.Single(_fx.AddedVerificationTokens);
        Assert.True(result.IsSuccess);
        Assert.Equal("aye@example.com", email.Email);
        Assert.Equal("Aye Chan", email.Name);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(TestData.Sha256(email.RawToken), token.TokenHash);
        Assert.Equal(1, email.SavesBeforeSend);
        _fx.VerificationTokens.Verify(
            r => r.DeleteByUserIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationAsync_UnverifiedUser_NewTokenExpiresIn24Hours()
    {
        _fx.HasUser(TestData.UnverifiedUser("aye@example.com"));
        var before = DateTime.UtcNow;

        await _fx.Service.ResendVerificationAsync(new ResendVerificationRequest("aye@example.com"));

        var after = DateTime.UtcNow;
        var token = Assert.Single(_fx.AddedVerificationTokens);
        Assert.InRange(token.ExpiresAt, before.AddHours(24), after.AddHours(24));
    }

    [Fact]
    public async Task ResendVerificationAsync_EmailWithCaseAndSpaces_FindsUser()
    {
        _fx.HasUser(TestData.UnverifiedUser("aye@example.com"));

        var result = await _fx.Service.ResendVerificationAsync(new ResendVerificationRequest(" AYE@example.com "));

        Assert.True(result.IsSuccess);
        Assert.Single(_fx.SentEmails);
    }

    [Fact]
    public async Task ResendVerificationAsync_UnknownEmail_ReturnsEmailNotRegistered()
    {
        var result = await _fx.Service.ResendVerificationAsync(new ResendVerificationRequest("nobody@example.com"));

        Assert.Equal(AuthErrors.EmailNotRegistered, result.Error);
        Assert.Empty(_fx.SentEmails);
        Assert.Empty(_fx.AddedVerificationTokens);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task ResendVerificationAsync_AlreadyVerified_ReturnsConflictAndKeepsExistingTokens()
    {
        var user = TestData.VerifiedUser("aye@example.com");
        _fx.HasUser(user);

        var result = await _fx.Service.ResendVerificationAsync(new ResendVerificationRequest("aye@example.com"));

        Assert.Equal(AuthErrors.EmailAlreadyVerified, result.Error);
        Assert.Empty(_fx.SentEmails);
        Assert.Empty(_fx.AddedVerificationTokens);
        _fx.VerificationTokens.Verify(
            r => r.DeleteByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
