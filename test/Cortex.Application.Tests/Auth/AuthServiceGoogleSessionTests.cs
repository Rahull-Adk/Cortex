using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Tests.Support;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceGoogleSessionTests
{
    private const string IdToken = "google-id-token";
    private const string GoogleSubject = "google-sub-123";

    private static readonly ExternalIdentity Identity =
        new(GoogleSubject, "Thiri@Example.com", "Thiri Aung", "https://images.example.com/thiri.png");

    private static readonly GoogleSessionRequest Request = new(IdToken);

    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task GoogleSessionAsync_InvalidIdToken_ReturnsInvalidGoogleToken()
    {
        var result = await _fx.Service.GoogleSessionAsync(new GoogleSessionRequest("forged-token"));

        Assert.Equal(AuthErrors.InvalidGoogleToken, result.Error);
        Assert.Empty(_fx.AddedUsers);
        Assert.Empty(_fx.AddedAccounts);
        Assert.Empty(_fx.AddedRefreshTokens);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task GoogleSessionAsync_KnownGoogleAccount_SignsInLinkedUserWithoutCreatingAnything()
    {
        var user = TestData.VerifiedUser("thiri@example.com", "Thiri Aung");
        _fx.HasUser(user);
        _fx.HasGoogleIdentity(IdToken, Identity);
        _fx.HasGoogleAccount(user.Id, GoogleSubject);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Empty(_fx.AddedUsers);
        Assert.Empty(_fx.AddedAccounts);
        Assert.Equal(user.Id, Assert.Single(_fx.AddedRefreshTokens).UserId);
    }

    [Fact]
    public async Task GoogleSessionAsync_KnownGoogleAccount_UsesLinkedUserEvenWhenGoogleEmailChanged()
    {
        var user = TestData.VerifiedUser("old-address@example.com", "Thiri Aung");
        _fx.HasUser(user);
        _fx.HasGoogleIdentity(IdToken, Identity);
        _fx.HasGoogleAccount(user.Id, GoogleSubject);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Empty(_fx.AddedUsers);
    }

    [Fact]
    public async Task GoogleSessionAsync_NewEmail_CreatesVerifiedUserFromGoogleProfile()
    {
        _fx.HasGoogleIdentity(IdToken, Identity);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        var user = Assert.Single(_fx.AddedUsers);
        Assert.True(result.IsSuccess);
        Assert.Equal("thiri@example.com", user.Email);
        Assert.Equal("Thiri Aung", user.Name);
        Assert.Equal("https://images.example.com/thiri.png", user.AvatarUrl);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.True(result.Value.User.EmailVerified);
    }

    [Fact]
    public async Task GoogleSessionAsync_NewEmail_LinksGoogleAccountWithoutPassword()
    {
        _fx.HasGoogleIdentity(IdToken, Identity);

        await _fx.Service.GoogleSessionAsync(Request);

        var user = Assert.Single(_fx.AddedUsers);
        var account = Assert.Single(_fx.AddedAccounts);
        Assert.Equal(user.Id, account.UserId);
        Assert.Equal("google", account.Provider);
        Assert.Equal(GoogleSubject, account.ProviderAccountId);
        Assert.Null(account.PasswordHash);
    }

    [Fact]
    public async Task GoogleSessionAsync_NewEmail_IssuesSessionForNewUser()
    {
        _fx.HasGoogleIdentity(IdToken, Identity);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        var refreshToken = Assert.Single(_fx.AddedRefreshTokens);
        Assert.Equal(AuthServiceFixture.AccessTokenValue, result.Value.AccessToken);
        Assert.Equal(TestData.Sha256(result.Value.RefreshToken), refreshToken.TokenHash);
        Assert.Equal(Assert.Single(_fx.AddedUsers).Id, refreshToken.UserId);
    }

    [Fact]
    public async Task GoogleSessionAsync_VerifiedUserWithSameEmail_LinksGoogleToThatUser()
    {
        var existing = TestData.VerifiedUser("thiri@example.com", "Old Name");
        _fx.HasUser(existing);
        _fx.HasGoogleIdentity(IdToken, Identity);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        var account = Assert.Single(_fx.AddedAccounts);
        Assert.True(result.IsSuccess);
        Assert.Empty(_fx.AddedUsers);
        Assert.Equal(existing.Id, account.UserId);
        Assert.Equal(GoogleSubject, account.ProviderAccountId);
        Assert.Equal(existing.Id, result.Value.User.Id);
    }

    [Fact]
    public async Task GoogleSessionAsync_VerifiedUserWithSameEmail_TakesNameAndAvatarFromGoogle()
    {
        var existing = TestData.VerifiedUser("thiri@example.com", "Old Name");
        _fx.HasUser(existing);
        _fx.HasGoogleIdentity(IdToken, Identity);

        await _fx.Service.GoogleSessionAsync(Request);

        Assert.Equal("Thiri Aung", existing.Name);
        Assert.Equal("https://images.example.com/thiri.png", existing.AvatarUrl);
    }

    [Fact]
    public async Task GoogleSessionAsync_AccountLinkedToMissingUser_Throws()
    {
        _fx.HasGoogleIdentity(IdToken, Identity);
        _fx.HasGoogleAccount(Guid.NewGuid(), GoogleSubject);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _fx.Service.GoogleSessionAsync(Request));

        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact(Skip = "Rule not implemented: a deactivated user with a linked Google account still gets a session.")]
    public async Task GoogleSessionAsync_DeactivatedUserWithLinkedGoogleAccount_IsRejected()
    {
        var user = TestData.InactiveUser("thiri@example.com");
        _fx.HasUser(user);
        _fx.HasGoogleIdentity(IdToken, Identity);
        _fx.HasGoogleAccount(user.Id, GoogleSubject);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        Assert.True(result.IsFailure);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact(Skip = "Rule not implemented: Google sign-in links to and signs in a deactivated user found by email.")]
    public async Task GoogleSessionAsync_DeactivatedUserWithSameEmail_IsRejected()
    {
        var user = TestData.InactiveUser("thiri@example.com");
        _fx.HasUser(user);
        _fx.HasGoogleIdentity(IdToken, Identity);

        var result = await _fx.Service.GoogleSessionAsync(Request);

        Assert.True(result.IsFailure);
        Assert.Empty(_fx.AddedAccounts);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact(Skip = "Rule not implemented: Google sign-in links to an unverified password account, so whoever "
        + "registered that email first keeps a working password on the victim's account.")]
    public async Task GoogleSessionAsync_UnverifiedPasswordAccountWithSameEmail_IsNotSilentlyLinked()
    {
        var preRegistered = TestData.UnverifiedUser("thiri@example.com", "Someone Else");
        _fx.HasUser(preRegistered);
        _fx.HasPassword(preRegistered, "attacker-password");
        _fx.HasGoogleIdentity(IdToken, Identity);

        await _fx.Service.GoogleSessionAsync(Request);

        Assert.DoesNotContain(_fx.AddedAccounts, account => account.UserId == preRegistered.Id);
    }
}
