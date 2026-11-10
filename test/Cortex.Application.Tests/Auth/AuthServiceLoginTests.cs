using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Tests.Support;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceLoginTests
{
    private const string Password = "S3cret-pass";

    private readonly AuthServiceFixture _fx = new();

    [Fact]
    public async Task LoginAsync_VerifiedUserWithCorrectPassword_ReturnsSession()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthServiceFixture.AccessTokenValue, result.Value.AccessToken);
        Assert.Equal(AuthServiceFixture.AccessTokenExpiresAt, result.Value.AccessTokenExpiresAt);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(user.Email, result.Value.User.Email);
        Assert.True(result.Value.User.EmailVerified);
    }

    [Fact]
    public async Task LoginAsync_Success_StoresOnlyHashOfReturnedRefreshToken()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password));

        var stored = Assert.Single(_fx.AddedRefreshTokens);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.Equal(TestData.Sha256(result.Value.RefreshToken), stored.TokenHash);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(stored.ExpiresAt, result.Value.RefreshTokenExpiresAt);
        Assert.True(stored.IsActive);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task LoginAsync_Success_RefreshTokenLastsForConfiguredLifetime()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);
        var before = DateTime.UtcNow;

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password));

        var after = DateTime.UtcNow;
        Assert.InRange(
            result.Value.RefreshTokenExpiresAt,
            before.Add(AuthServiceFixture.RefreshLifetime),
            after.Add(AuthServiceFixture.RefreshLifetime));
    }

    [Fact]
    public async Task LoginAsync_TwoSignIns_StartSeparateTokenFamilies()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);
        var request = new LoginRequest(user.Email, Password);

        var first = await _fx.Service.LoginAsync(request);
        var second = await _fx.Service.LoginAsync(request);

        Assert.Equal(2, _fx.AddedRefreshTokens.Count);
        Assert.NotEqual(_fx.AddedRefreshTokens[0].FamilyId, _fx.AddedRefreshTokens[1].FamilyId);
        Assert.NotEqual(first.Value.RefreshToken, second.Value.RefreshToken);
    }

    [Fact]
    public async Task LoginAsync_EmailWithCaseAndSpaces_FindsUser()
    {
        var user = TestData.VerifiedUser("owner@example.com");
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest("  Owner@Example.COM ", Password));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        var result = await _fx.Service.LoginAsync(new LoginRequest("nobody@example.com", Password));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, "wrong-password"));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task LoginAsync_PasswordDiffersOnlyByCase_ReturnsInvalidCredentials()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password.ToUpperInvariant()));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUserWithCorrectPassword_ReturnsInvalidCredentials()
    {
        var user = TestData.InactiveUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task LoginAsync_UserWithoutPasswordAccount_ReturnsInvalidCredentials()
    {
        var googleOnlyUser = TestData.VerifiedUser();
        _fx.HasUser(googleOnlyUser);

        var result = await _fx.Service.LoginAsync(new LoginRequest(googleOnlyUser.Email, Password));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedUserWithCorrectPassword_ReturnsEmailNotVerified()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, Password));

        Assert.Equal(AuthErrors.EmailNotVerified, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedUserWithWrongPassword_DoesNotRevealVerificationState()
    {
        var user = TestData.UnverifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var result = await _fx.Service.LoginAsync(new LoginRequest(user.Email, "wrong-password"));

        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmailAndWrongPassword_ReturnTheSameError()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasPassword(user, Password);

        var unknownEmail = await _fx.Service.LoginAsync(new LoginRequest("nobody@example.com", Password));
        var wrongPassword = await _fx.Service.LoginAsync(new LoginRequest(user.Email, "wrong-password"));

        Assert.Equal(unknownEmail.Error, wrongPassword.Error);
    }
}
