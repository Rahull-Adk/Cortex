using Cortex.Domain.Entities;

namespace Cortex.Application.Tests.Domain;

public class UserTests
{
    [Fact]
    public void Create_ValidInput_StartsActiveAndUnverifiedWithNormalizedFields()
    {
        var user = User.Create("  Aye Chan ", "  Aye@Example.COM ");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Aye Chan", user.Name);
        Assert.Equal("aye@example.com", user.Email);
        Assert.True(user.IsActive);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Null(user.AvatarUrl);
    }

    [Theory]
    [InlineData("", "aye@example.com")]
    [InlineData("   ", "aye@example.com")]
    [InlineData("Aye Chan", "")]
    [InlineData("Aye Chan", "   ")]
    public void Create_BlankNameOrEmail_Throws(string name, string email)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create(name, email));
    }

    [Fact]
    public void MarkEmailVerified_UnverifiedUser_SetsVerifiedTime()
    {
        var user = User.Create("Aye Chan", "aye@example.com");
        var before = DateTimeOffset.UtcNow;

        user.MarkEmailVerified();

        Assert.NotNull(user.EmailVerifiedAt);
        Assert.InRange(user.EmailVerifiedAt.Value, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void MarkEmailVerified_AlreadyVerified_KeepsOriginalVerifiedTime()
    {
        var user = User.Create("Aye Chan", "aye@example.com");
        user.MarkEmailVerified();
        var firstVerifiedAt = user.EmailVerifiedAt;

        user.MarkEmailVerified();

        Assert.Equal(firstVerifiedAt, user.EmailVerifiedAt);
    }

    [Fact]
    public void Deactivate_ThenReactivate_TogglesActiveState()
    {
        var user = User.Create("Aye Chan", "aye@example.com");

        user.Deactivate();
        var afterDeactivate = user.IsActive;
        user.Reactivate();

        Assert.False(afterDeactivate);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Deactivate_VerifiedUser_KeepsEmailVerified()
    {
        var user = User.Create("Aye Chan", "aye@example.com");
        user.MarkEmailVerified();

        user.Deactivate();

        Assert.NotNull(user.EmailVerifiedAt);
    }

    [Fact]
    public void UpdateProfile_ValidInput_TrimsNameAndSetsAvatar()
    {
        var user = User.Create("Aye Chan", "aye@example.com");

        user.UpdateProfile("  Aye Chan Moe ", "https://images.example.com/aye.png");

        Assert.Equal("Aye Chan Moe", user.Name);
        Assert.Equal("https://images.example.com/aye.png", user.AvatarUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateProfile_BlankName_ThrowsAndKeepsName(string name)
    {
        var user = User.Create("Aye Chan", "aye@example.com");

        Assert.ThrowsAny<ArgumentException>(() => user.UpdateProfile(name, null));

        Assert.Equal("Aye Chan", user.Name);
    }
}
