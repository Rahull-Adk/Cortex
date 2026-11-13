using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Domain;

public class OrganizationInvitationTests
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private static OrganizationInvitation Invitation(OrganizationRole role = OrganizationRole.Member) =>
        OrganizationInvitation.Create(Guid.NewGuid(), "  Staff@Example.COM ", role, "hash-1", Guid.NewGuid(), Week);

    [Fact]
    public void Create_NormalizesEmailAndSetsExpiry()
    {
        var invitation = Invitation();

        Assert.Equal("staff@example.com", invitation.Email);
        Assert.Equal(OrganizationRole.Member, invitation.Role);
        Assert.Equal("hash-1", invitation.TokenHash);
        Assert.False(invitation.IsExpired(DateTime.UtcNow));
        Assert.True(invitation.IsExpired(DateTime.UtcNow.Add(Week).AddSeconds(1)));
    }

    [Theory]
    [InlineData(OrganizationRole.Owner)]
    [InlineData(OrganizationRole.None)]
    public void Create_RoleThatIsNotAdminOrMember_Throws(OrganizationRole role)
    {
        Assert.Throws<ArgumentException>(() => Invitation(role));
    }

    [Fact]
    public void Create_WithoutOrganizationOrInviter_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(
            Guid.Empty, "staff@example.com", OrganizationRole.Member, "hash", Guid.NewGuid(), Week));
        Assert.Throws<ArgumentException>(() => OrganizationInvitation.Create(
            Guid.NewGuid(), "staff@example.com", OrganizationRole.Member, "hash", Guid.Empty, Week));
    }

    [Fact]
    public void Create_NonPositiveValidity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationInvitation.Create(
            Guid.NewGuid(), "staff@example.com", OrganizationRole.Member, "hash", Guid.NewGuid(), TimeSpan.Zero));
    }

    [Fact]
    public void Renew_ReplacesTokenRoleAndInviter()
    {
        var invitation = Invitation();
        var newInviter = Guid.NewGuid();

        invitation.Renew(OrganizationRole.Admin, "hash-2", newInviter, Week);

        Assert.Equal("hash-2", invitation.TokenHash);
        Assert.Equal(OrganizationRole.Admin, invitation.Role);
        Assert.Equal(newInviter, invitation.InvitedByUserId);
    }

    [Fact]
    public void IsExpired_AtExactExpiry_IsTrue()
    {
        var invitation = Invitation();

        Assert.True(invitation.IsExpired(invitation.ExpiresAt));
        Assert.False(invitation.IsExpired(invitation.ExpiresAt.AddTicks(-1)));
    }
}
