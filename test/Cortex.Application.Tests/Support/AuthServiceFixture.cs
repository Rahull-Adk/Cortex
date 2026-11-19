using Cortex.Application.Auth;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.Application.Tests.Support;

internal sealed class AuthServiceFixture
{
    public const string AccessTokenValue = "access-token";
    public static readonly DateTime AccessTokenExpiresAt = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    public Mock<IUserRepository> Users { get; } = new();
    public Mock<IUserAccountRepository> Accounts { get; } = new();
    public Mock<IEmailVerificationTokenRepository> VerificationTokens { get; } = new();
    public Mock<IRefreshTokenRepository> RefreshTokens { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IPasswordHasher> PasswordHasher { get; } = new();
    public Mock<ITokenService> TokenService { get; } = new();
    public Mock<IExternalIdentityTokenValidator> GoogleValidator { get; } = new();
    public Mock<IEmailService> EmailService { get; } = new();
    public Mock<IOrganizationInvitationRepository> Invitations { get; } = new();
    public Mock<IOrganizationMembershipRepository> Memberships { get; } = new();
    public FakeClock Clock { get; } = new();

    public List<OrganizationMembership> AddedMemberships { get; } = [];
    public List<OrganizationInvitation> RemovedInvitations { get; } = [];
    public List<Guid> ClearedVerificationTokensFor { get; } = [];
    public int DiscardCount { get; private set; }

    public List<User> AddedUsers { get; } = [];
    public List<UserAccount> AddedAccounts { get; } = [];
    public List<EmailVerificationToken> AddedVerificationTokens { get; } = [];
    public List<EmailVerificationToken> RemovedVerificationTokens { get; } = [];
    public List<RefreshToken> AddedRefreshTokens { get; } = [];
    public List<Guid> RevokedFamilies { get; } = [];
    public List<SentEmail> SentEmails { get; } = [];
    public int SaveCount { get; private set; }

    public AuthService Service { get; }

    public AuthServiceFixture()
    {
        Users.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(AddedUsers.Add);
        Accounts.Setup(r => r.Add(It.IsAny<UserAccount>())).Callback<UserAccount>(AddedAccounts.Add);
        VerificationTokens.Setup(r => r.Add(It.IsAny<EmailVerificationToken>()))
            .Callback<EmailVerificationToken>(AddedVerificationTokens.Add);
        VerificationTokens.Setup(r => r.Remove(It.IsAny<EmailVerificationToken>()))
            .Callback<EmailVerificationToken>(RemovedVerificationTokens.Add);
        RefreshTokens.Setup(r => r.Add(It.IsAny<RefreshToken>())).Callback<RefreshToken>(AddedRefreshTokens.Add);
        RefreshTokens.Setup(r => r.RevokeFamilyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((familyId, _) => RevokedFamilies.Add(familyId))
            .Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => SaveCount++)
            .Returns(Task.CompletedTask);
        PasswordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(HashOf);
        TokenService.SetupGet(t => t.RefreshTokenLifetime).Returns(RefreshLifetime);
        TokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken(AccessTokenValue, AccessTokenExpiresAt));
        EmailService
            .Setup(e => e.SendVerificationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((email, name, rawToken, _) =>
                SentEmails.Add(new SentEmail(email, name, rawToken, SaveCount)))
            .Returns(Task.CompletedTask);
        VerificationTokens.Setup(r => r.DeleteByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((userId, _) => ClearedVerificationTokensFor.Add(userId))
            .Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.DiscardChanges()).Callback(() => DiscardCount++);
        Memberships.Setup(r => r.Add(It.IsAny<OrganizationMembership>()))
            .Callback<OrganizationMembership>(AddedMemberships.Add);
        Invitations.Setup(r => r.Remove(It.IsAny<OrganizationInvitation>()))
            .Callback<OrganizationInvitation>(RemovedInvitations.Add);

        Service = new AuthService(
            Users.Object,
            Accounts.Object,
            VerificationTokens.Object,
            RefreshTokens.Object,
            UnitOfWork.Object,
            PasswordHasher.Object,
            TokenService.Object,
            GoogleValidator.Object,
            EmailService.Object,
            Invitations.Object,
            Memberships.Object,
            Clock,
            NullLogger<AuthService>.Instance);
    }

    public OrganizationInvitation HasInvitation(
        string rawToken,
        string email,
        OrganizationRole role = OrganizationRole.Member,
        Organization? organization = null)
    {
        organization ??= Organization.Create("Shwe Store", "shwe-store", Guid.NewGuid());
        var invitation = OrganizationInvitation.Create(
            organization.Id, email, role, TestData.Sha256(rawToken), Guid.NewGuid(), TimeSpan.FromDays(7));

        // Navigations have private setters that only EF populates.
        typeof(OrganizationInvitation).GetProperty(nameof(OrganizationInvitation.Organization))!
            .SetValue(invitation, organization);
        Invitations.Setup(r => r.FindByTokenHashAsync(invitation.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        return invitation;
    }

    public void SaveHitsUniqueConstraint() =>
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintException(new InvalidOperationException("duplicate key")));

    public static string HashOf(string password) => $"hashed:{password}";

    public void HasUser(User user)
    {
        Users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Users.Setup(r => r.FindByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Users.Setup(r => r.ExistsByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public UserAccount HasPassword(User user, string password)
    {
        var account = UserAccount.CreateCredentials(user.Id, HashOf(password));
        Accounts.Setup(r => r.FindCredentialsByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        PasswordHasher.Setup(h => h.Verify(password, HashOf(password))).Returns(true);
        return account;
    }

    public void HasGoogleIdentity(string idToken, ExternalIdentity identity) =>
        GoogleValidator.Setup(v => v.ValidateGoogleIdTokenAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(identity);

    public UserAccount HasGoogleAccount(Guid userId, string providerAccountId)
    {
        var account = UserAccount.CreateOAuth(userId, "google", providerAccountId);
        Accounts.Setup(r => r.FindByProviderAsync("google", providerAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    public void HasRefreshToken(RefreshToken token) =>
        RefreshTokens.Setup(r => r.FindByTokenHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

    public void HasVerificationToken(EmailVerificationToken token) =>
        VerificationTokens.Setup(r => r.FindByTokenHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
}

internal sealed record SentEmail(string Email, string Name, string RawToken, int SavesBeforeSend);
