using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Organizations;
using Cortex.Application.Tests.Support;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;

namespace Cortex.Application.Tests.Auth;

public class AuthServiceRegisterInvitedTests
{
    private const string RawToken = "raw-invitation-token";
    private const string StaffEmail = "staff@example.com";

    private readonly AuthServiceFixture _fx = new();

    private static readonly RegisterInvitedRequest ValidRequest = new(RawToken, "New Staff", "S3cret-pass");

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin)]
    public async Task RegisterInvitedAsync_NewEmail_CreatesVerifiedUserAndJoinsWithInvitedRole(OrganizationRole role)
    {
        var invitation = _fx.HasInvitation(RawToken, StaffEmail, role);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_fx.AddedUsers);
        Assert.Equal(StaffEmail, user.Email);
        Assert.Equal("New Staff", user.Name);
        Assert.True(user.EmailVerifiedAt.HasValue);

        var membership = Assert.Single(_fx.AddedMemberships);
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(invitation.OrganizationId, membership.OrganizationId);
        Assert.Equal(role, membership.Role);
        Assert.Same(invitation, Assert.Single(_fx.RemovedInvitations));
    }

    [Fact]
    public async Task RegisterInvitedAsync_NewEmail_StartsSessionInOneSaveWithoutVerificationEmail()
    {
        _fx.HasInvitation(RawToken, StaffEmail);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(AuthServiceFixture.AccessTokenValue, result.Value.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.True(result.Value.User.EmailVerified);
        Assert.Equal(StaffEmail, result.Value.User.Email);
        Assert.Single(_fx.AddedRefreshTokens);
        Assert.Equal(1, _fx.SaveCount);
        Assert.Empty(_fx.SentEmails);
        Assert.Empty(_fx.AddedVerificationTokens);
    }

    [Fact]
    public async Task RegisterInvitedAsync_NewEmail_StoresHashedPassword()
    {
        _fx.HasInvitation(RawToken, StaffEmail);

        await _fx.Service.RegisterInvitedAsync(ValidRequest);

        var account = Assert.Single(_fx.AddedAccounts);
        Assert.Equal("credentials", account.Provider);
        Assert.Equal(AuthServiceFixture.HashOf("S3cret-pass"), account.PasswordHash);
    }

    [Fact]
    public async Task RegisterInvitedAsync_TokenWithSurroundingSpaces_IsAccepted()
    {
        _fx.HasInvitation(RawToken, StaffEmail);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest with { Token = $"  {RawToken} " });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterInvitedAsync_UnverifiedAccount_IsTakenOverWithNewPassword()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        var squatter = TestData.UnverifiedUser(StaffEmail, "Someone Else");
        _fx.HasUser(squatter);
        var account = _fx.HasPassword(squatter, "old-password");

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.True(result.IsSuccess);
        Assert.Equal(squatter.Id, result.Value.User.Id);
        Assert.True(squatter.EmailVerifiedAt.HasValue);
        Assert.Equal("New Staff", squatter.Name);
        Assert.Equal(AuthServiceFixture.HashOf("S3cret-pass"), account.PasswordHash);
        Assert.Empty(_fx.AddedUsers);
        Assert.Empty(_fx.AddedAccounts);
        Assert.Equal(squatter.Id, Assert.Single(_fx.ClearedVerificationTokensFor));
        Assert.Equal(squatter.Id, Assert.Single(_fx.AddedMemberships).UserId);
        Assert.Single(_fx.RemovedInvitations);
    }

    [Fact]
    public async Task RegisterInvitedAsync_UnverifiedAccountWithoutPassword_GetsCredentials()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        var existing = TestData.UnverifiedUser(StaffEmail);
        _fx.HasUser(existing);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(_fx.AddedAccounts);
        Assert.Equal(existing.Id, account.UserId);
        Assert.Equal(AuthServiceFixture.HashOf("S3cret-pass"), account.PasswordHash);
    }

    [Fact]
    public async Task RegisterInvitedAsync_VerifiedAccount_ReturnsConflictAndChangesNothing()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        var existing = TestData.VerifiedUser(StaffEmail, "Existing");
        _fx.HasUser(existing);
        var account = _fx.HasPassword(existing, "old-password");

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        Assert.Equal("Existing", existing.Name);
        Assert.Equal(AuthServiceFixture.HashOf("old-password"), account.PasswordHash);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Empty(_fx.RemovedInvitations);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RegisterInvitedAsync_DeactivatedAccount_ReturnsConflict()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        _fx.HasUser(TestData.InactiveUser(StaffEmail));

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        Assert.Empty(_fx.AddedMemberships);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RegisterInvitedAsync_UnknownToken_ReturnsInvalidInvitation()
    {
        _fx.HasInvitation(RawToken, StaffEmail);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest with { Token = "another-token" });

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
        Assert.Empty(_fx.AddedUsers);
        Assert.Equal(0, _fx.SaveCount);
    }

    [Fact]
    public async Task RegisterInvitedAsync_ExpiredInvitation_ReturnsInvalidInvitation()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        _fx.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
        Assert.Empty(_fx.AddedUsers);
    }

    [Fact]
    public async Task RegisterInvitedAsync_DeactivatedBusiness_ReturnsInvalidInvitation()
    {
        var organization = Organization.Create("Shwe Store", "shwe-store", Guid.NewGuid());
        organization.Deactivate();
        _fx.HasInvitation(RawToken, StaffEmail, organization: organization);

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(OrganizationErrors.InvalidInvitation, result.Error);
        Assert.Empty(_fx.AddedUsers);
    }

    [Fact]
    public async Task RegisterInvitedAsync_EmailRegisteredConcurrently_ReturnsConflictAndDiscardsChanges()
    {
        _fx.HasInvitation(RawToken, StaffEmail);
        _fx.SaveHitsUniqueConstraint();

        var result = await _fx.Service.RegisterInvitedAsync(ValidRequest);

        Assert.Equal(AuthErrors.EmailAlreadyRegistered, result.Error);
        Assert.Equal(1, _fx.DiscardCount);
    }
}
