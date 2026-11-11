using Cortex.Application.Authorization;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Application.Organizations;
using Cortex.Domain.Entities;
using Cortex.Domain.Enums;
using Moq;

namespace Cortex.Application.Tests.Support;

internal sealed class OrganizationFixture
{
    private readonly List<OrganizationMembership> _stored = [];
    private readonly HashSet<string> _takenSlugs = [];

    public Mock<IOrganizationRepository> Organizations { get; } = new();
    public Mock<IOrganizationMembershipRepository> Memberships { get; } = new();
    public Mock<IUserRepository> Users { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public List<Organization> AddedOrganizations { get; } = [];
    public List<OrganizationMembership> AddedMemberships { get; } = [];
    public List<OrganizationMembership> RemovedMemberships { get; } = [];
    public List<string> CheckedSlugs { get; } = [];
    public int SaveCount { get; private set; }
    public int DiscardCount { get; private set; }

    public OrganizationService OrganizationService { get; }
    public MemberService MemberService { get; }

    public OrganizationFixture()
    {
        Organizations.Setup(r => r.Add(It.IsAny<Organization>())).Callback<Organization>(AddedOrganizations.Add);
        Organizations.Setup(r => r.ExistsBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string slug, CancellationToken _) =>
            {
                CheckedSlugs.Add(slug);
                return _takenSlugs.Contains(slug);
            });

        // Mirrors the repository contract: FindActive hides inactive businesses and users.
        Memberships.Setup(r => r.FindActiveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, Guid userId, CancellationToken _) => _stored.FirstOrDefault(m =>
                m.OrganizationId == organizationId
                && m.UserId == userId
                && m.Organization.IsActive
                && m.User.IsActive));
        Memberships.Setup(r => r.FindWithUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, Guid userId, CancellationToken _) => _stored.FirstOrDefault(m =>
                m.OrganizationId == organizationId && m.UserId == userId));
        Memberships.Setup(r => r.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) => _stored
                .Where(m => m.UserId == userId && m.Organization.IsActive)
                .OrderBy(m => m.Organization.Name)
                .ToList());
        Memberships.Setup(r => r.ListByOrganizationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid organizationId, CancellationToken _) => _stored
                .Where(m => m.OrganizationId == organizationId)
                .ToList());
        Memberships.Setup(r => r.Add(It.IsAny<OrganizationMembership>()))
            .Callback<OrganizationMembership>(AddedMemberships.Add);
        Memberships.Setup(r => r.Remove(It.IsAny<OrganizationMembership>()))
            .Callback<OrganizationMembership>(membership =>
            {
                RemovedMemberships.Add(membership);
                _stored.Remove(membership);
            });

        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => SaveCount++)
            .Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.DiscardChanges()).Callback(() => DiscardCount++);

        var authorizer = new OrganizationAuthorizer(Memberships.Object);
        OrganizationService = new OrganizationService(
            Organizations.Object, Memberships.Object, Users.Object, UnitOfWork.Object, authorizer);
        MemberService = new MemberService(Memberships.Object, Users.Object, UnitOfWork.Object, authorizer);
    }

    public User HasUser(User user)
    {
        Users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Users.Setup(r => r.FindByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    public User HasUser(string email, string name = "Staff") => HasUser(TestData.VerifiedUser(email, name));

    public void HasTakenSlug(string slug) => _takenSlugs.Add(slug);

    public Organization HasOrganization(User owner, string name = "Shwe Store", string slug = "shwe-store")
    {
        HasUser(owner);
        var organization = Organization.Create(name, slug, owner.Id);
        _takenSlugs.Add(organization.Slug);
        Store(organization.Memberships.Single(), organization, owner);
        return organization;
    }

    public OrganizationMembership HasMember(Organization organization, User user, OrganizationRole role)
    {
        HasUser(user);
        var membership = OrganizationMembership.Create(organization.Id, user.Id, role);
        Store(membership, organization, user);
        return membership;
    }

    public void SaveHitsUniqueConstraint() =>
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintException(new InvalidOperationException("duplicate key")));

    // Navigations have private setters that only EF populates.
    private void Store(OrganizationMembership membership, Organization organization, User user)
    {
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.Organization))!
            .SetValue(membership, organization);
        typeof(OrganizationMembership).GetProperty(nameof(OrganizationMembership.User))!
            .SetValue(membership, user);
        _stored.Add(membership);
    }
}
