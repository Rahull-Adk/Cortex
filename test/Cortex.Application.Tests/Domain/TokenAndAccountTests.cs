using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;

namespace Cortex.Application.Tests.Domain;

public class RefreshTokenTests
{
    private static RefreshToken NewToken() =>
        RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "token-hash", TimeSpan.FromDays(7));

    [Fact]
    public void Create_ValidInput_IsActiveAndExpiresAfterLifetime()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var token = RefreshToken.Create(userId, familyId, "token-hash", TimeSpan.FromDays(7));

        Assert.Equal(userId, token.UserId);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Equal("token-hash", token.TokenHash);
        Assert.InRange(token.ExpiresAt, before.AddDays(7), DateTime.UtcNow.AddDays(7));
        Assert.True(token.IsActive);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveLifetime_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "token-hash", TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTokenHash_Throws(string tokenHash)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), tokenHash, TimeSpan.FromDays(7)));
    }

    [Fact]
    public void Revoke_ActiveToken_BecomesRevokedAndRecordsReplacement()
    {
        var token = NewToken();
        var replacementId = Guid.NewGuid();

        token.Revoke(replacementId);

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(replacementId, token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoke_WithoutReplacement_LeavesReplacementEmpty()
    {
        var token = NewToken();

        token.Revoke();

        Assert.True(token.IsRevoked);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoke_AlreadyRevoked_KeepsFirstRevocation()
    {
        var token = NewToken();
        var firstReplacementId = Guid.NewGuid();
        token.Revoke(firstReplacementId);
        var firstRevokedAt = token.RevokedAt;

        token.Revoke(Guid.NewGuid());

        Assert.Equal(firstReplacementId, token.ReplacedByTokenId);
        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }

    [Fact]
    public void IsActive_ExpiredToken_IsFalseEvenThoughNotRevoked()
    {
        var token = TestData.ExpiredRefreshTokenFor(TestData.VerifiedUser(), "raw");

        Assert.True(token.IsExpired);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsActive);
    }
}

public class EmailVerificationTokenTests
{
    [Fact]
    public void Create_PositiveValidity_IsNotExpired()
    {
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var token = EmailVerificationToken.Create(userId, "token-hash", TimeSpan.FromHours(24));

        Assert.Equal(userId, token.UserId);
        Assert.Equal("token-hash", token.TokenHash);
        Assert.InRange(token.ExpiresAt, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsExpired_ValidityAlreadyPassed_IsTrue()
    {
        var token = EmailVerificationToken.Create(Guid.NewGuid(), "token-hash", TimeSpan.FromSeconds(-1));

        Assert.True(token.IsExpired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTokenHash_Throws(string tokenHash)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            EmailVerificationToken.Create(Guid.NewGuid(), tokenHash, TimeSpan.FromHours(24)));
    }
}

public class UserAccountTests
{
    [Fact]
    public void CreateCredentials_ValidHash_IsCredentialsAccountHoldingTheHash()
    {
        var userId = Guid.NewGuid();

        var account = UserAccount.CreateCredentials(userId, "password-hash");

        Assert.Equal(userId, account.UserId);
        Assert.Equal("credentials", account.Provider);
        Assert.Equal("password-hash", account.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateCredentials_BlankHash_Throws(string passwordHash)
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.CreateCredentials(Guid.NewGuid(), passwordHash));
    }

    [Fact]
    public void CreateOAuth_ValidInput_LowercasesProviderAndHasNoPassword()
    {
        var userId = Guid.NewGuid();

        var account = UserAccount.CreateOAuth(userId, "Google", "google-sub-123");

        Assert.Equal(userId, account.UserId);
        Assert.Equal("google", account.Provider);
        Assert.Equal("google-sub-123", account.ProviderAccountId);
        Assert.Null(account.PasswordHash);
    }

    [Theory]
    [InlineData("", "google-sub-123")]
    [InlineData("   ", "google-sub-123")]
    [InlineData("google", "")]
    [InlineData("google", "   ")]
    public void CreateOAuth_BlankProviderOrAccountId_Throws(string provider, string providerAccountId)
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.CreateOAuth(Guid.NewGuid(), provider, providerAccountId));
    }

    [Fact]
    public void UpdatePasswordHash_NewHash_ReplacesOldHash()
    {
        var account = UserAccount.CreateCredentials(Guid.NewGuid(), "old-hash");

        account.UpdatePasswordHash("new-hash");

        Assert.Equal("new-hash", account.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdatePasswordHash_BlankHash_ThrowsAndKeepsOldHash(string passwordHash)
    {
        var account = UserAccount.CreateCredentials(Guid.NewGuid(), "old-hash");

        Assert.ThrowsAny<ArgumentException>(() => account.UpdatePasswordHash(passwordHash));

        Assert.Equal("old-hash", account.PasswordHash);
    }
}
