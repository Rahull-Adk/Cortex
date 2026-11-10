using Cortex.Infrastructure.Auth;

namespace Cortex.Infrastructure.Tests.Auth;

public class BcryptPasswordHasherTests
{
    private const string Password = "S3cret-pass";

    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_Password_DoesNotContainThePassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void Hash_Password_UsesBcryptWithWorkFactor12()
    {
        var hash = _hasher.Hash(Password);

        Assert.Matches(@"^\$2[aby]\$12\$", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Password);

        var verified = _hasher.Verify(Password, hash);

        Assert.True(verified);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("s3cret-pass")]
    [InlineData("S3cret-pass ")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        var hash = _hasher.Hash(Password);

        var verified = _hasher.Verify(attempt, hash);

        Assert.False(verified);
    }

    [Fact]
    public void Verify_PasswordAgainstAnotherPasswordsHash_ReturnsFalse()
    {
        var otherHash = _hasher.Hash("another-password");

        var verified = _hasher.Verify(Password, otherHash);

        Assert.False(verified);
    }

    [Fact]
    public void Verify_NonAsciiPassword_RoundTrips()
    {
        const string password = "စကားဝှက်-pässwörd-🔐";
        var hash = _hasher.Hash(password);

        var verified = _hasher.Verify(password, hash);

        Assert.True(verified);
    }
}
