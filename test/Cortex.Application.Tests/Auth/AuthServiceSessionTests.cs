using Cortex.Application.Auth;
using Cortex.Application.Common;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Moq;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceSessionTests
{
    private const string RawToken = "raw-refresh-token";

    private readonly AuthServiceFixture _fx = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshAsync_MissingToken_ReturnsInvalidRefreshToken(string? rawToken)
    {
        var result = await _fx.Service.RefreshAsync(rawToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_ReturnsInvalidRefreshToken()
    {
        var result = await _fx.Service.RefreshAsync("never-issued");

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_fx.AddedRefreshTokens);
        Assert.Empty(_fx.RevokedFamilies);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewSessionForSameUser()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasRefreshToken(TestData.RefreshTokenFor(user, RawToken));

        var result = await _fx.Service.RefreshAsync(RawToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(AuthServiceFixture.AccessTokenValue, result.Value.AccessToken);
        Assert.NotEqual(RawToken, result.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesWithinTheSameFamily()
    {
        var user = TestData.VerifiedUser();
        var oldToken = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasUser(user);
        _fx.HasRefreshToken(oldToken);

        var result = await _fx.Service.RefreshAsync(RawToken);

        var newToken = Assert.Single(_fx.AddedRefreshTokens);
        Assert.Equal(oldToken.FamilyId, newToken.FamilyId);
        Assert.Equal(TestData.Sha256(result.Value.RefreshToken), newToken.TokenHash);
        Assert.True(newToken.IsActive);
        Assert.Empty(_fx.RevokedFamilies);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesUsedTokenAndPointsItAtReplacement()
    {
        var user = TestData.VerifiedUser();
        var oldToken = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasUser(user);
        _fx.HasRefreshToken(oldToken);

        await _fx.Service.RefreshAsync(RawToken);

        var newToken = Assert.Single(_fx.AddedRefreshTokens);
        Assert.True(oldToken.IsRevoked);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);
    }

    [Fact]
    public async Task RefreshAsync_SameTokenUsedTwice_SecondUseFailsAndRevokesFamily()
    {
        var user = TestData.VerifiedUser();
        var oldToken = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasUser(user);
        _fx.HasRefreshToken(oldToken);

        var first = await _fx.Service.RefreshAsync(RawToken);
        var second = await _fx.Service.RefreshAsync(RawToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(AuthErrors.InvalidRefreshToken, second.Error);
        Assert.Equal(oldToken.FamilyId, Assert.Single(_fx.RevokedFamilies));
        Assert.Single(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsInvalidAndRevokesWholeFamily()
    {
        var user = TestData.VerifiedUser();
        var token = TestData.RefreshTokenFor(user, RawToken);
        token.Revoke();
        _fx.HasUser(user);
        _fx.HasRefreshToken(token);

        var result = await _fx.Service.RefreshAsync(RawToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Equal(token.FamilyId, Assert.Single(_fx.RevokedFamilies));
        Assert.Empty(_fx.AddedRefreshTokens);
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsInvalidWithoutRevokingFamily()
    {
        var user = TestData.VerifiedUser();
        _fx.HasUser(user);
        _fx.HasRefreshToken(TestData.ExpiredRefreshTokenFor(user, RawToken));

        var result = await _fx.Service.RefreshAsync(RawToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Empty(_fx.RevokedFamilies);
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deactivated")]
    [InlineData("unverified")]
    public async Task RefreshAsync_UserNoLongerEligible_ReturnsInvalidAndRevokesFamily(string userState)
    {
        var user = userState switch
        {
            "deactivated" => TestData.InactiveUser(),
            "unverified" => TestData.UnverifiedUser(),
            _ => TestData.VerifiedUser(),
        };
        if (userState != "missing")
            _fx.HasUser(user);
        var token = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasRefreshToken(token);

        var result = await _fx.Service.RefreshAsync(RawToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Equal(token.FamilyId, Assert.Single(_fx.RevokedFamilies));
        Assert.Empty(_fx.AddedRefreshTokens);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentUseOfSameToken_DiscardsChangesRevokesFamilyAndFails()
    {
        var user = TestData.VerifiedUser();
        var token = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasUser(user);
        _fx.HasRefreshToken(token);
        _fx.UnitOfWork.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException(new InvalidOperationException()))
            .Returns(Task.CompletedTask);

        var result = await _fx.Service.RefreshAsync(RawToken);

        Assert.Equal(AuthErrors.InvalidRefreshToken, result.Error);
        Assert.Equal(token.FamilyId, Assert.Single(_fx.RevokedFamilies));
        _fx.UnitOfWork.Verify(u => u.DiscardChanges(), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LogoutAsync_MissingToken_DoesNothing(string? rawToken)
    {
        await _fx.Service.LogoutAsync(rawToken);

        Assert.Empty(_fx.RevokedFamilies);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_DoesNotRevokeAnything()
    {
        await _fx.Service.LogoutAsync("never-issued");

        Assert.Empty(_fx.RevokedFamilies);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task LogoutAsync_KnownToken_RevokesItsFamilyAndSaves()
    {
        var user = TestData.VerifiedUser();
        var token = TestData.RefreshTokenFor(user, RawToken);
        _fx.HasRefreshToken(token);

        await _fx.Service.LogoutAsync(RawToken);

        Assert.Equal(token.FamilyId, Assert.Single(_fx.RevokedFamilies));
        Assert.Equal(1, _fx.SaveCount);
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_StillRevokesFamily()
    {
        var user = TestData.VerifiedUser();
        var token = TestData.RefreshTokenFor(user, RawToken);
        token.Revoke();
        _fx.HasRefreshToken(token);

        await _fx.Service.LogoutAsync(RawToken);

        Assert.Equal(token.FamilyId, Assert.Single(_fx.RevokedFamilies));
    }

    [Fact]
    public async Task GetCurrentUserAsync_ExistingUser_ReturnsProfile()
    {
        var user = TestData.VerifiedUser("aye@example.com", "Aye Chan");
        user.UpdateProfile("Aye Chan", "https://images.example.com/aye.png");
        _fx.HasUser(user);

        var result = await _fx.Service.GetCurrentUserAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.Id);
        Assert.Equal("Aye Chan", result.Value.Name);
        Assert.Equal("aye@example.com", result.Value.Email);
        Assert.True(result.Value.EmailVerified);
        Assert.Equal("https://images.example.com/aye.png", result.Value.AvatarUrl);
    }

    [Fact]
    public async Task GetCurrentUserAsync_UnknownUser_ReturnsUserNotFound()
    {
        var result = await _fx.Service.GetCurrentUserAsync(Guid.NewGuid());

        Assert.Equal(AuthErrors.UserNotFound, result.Error);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact(Skip = "Rule not implemented: a deactivated user with an unexpired access token still gets their profile.")]
    public async Task GetCurrentUserAsync_DeactivatedUser_IsRejected()
    {
        var user = TestData.InactiveUser();
        _fx.HasUser(user);

        var result = await _fx.Service.GetCurrentUserAsync(user.Id);

        Assert.True(result.IsFailure);
    }
}
