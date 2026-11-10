using Cortex.Domain.Enums;

namespace Cortex.Domain.Entities;

public class OrganizationMembership
{
    private OrganizationMembership() { }

    private OrganizationMembership(Guid id, Guid organizationId, Guid userId, OrganizationRole role)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public Organization Organization { get; private set; } = default!;
    public User User { get; private set; } = default!;

    public static OrganizationMembership Create(Guid organizationId, Guid userId, OrganizationRole role)
    {
        return new OrganizationMembership(Guid.NewGuid(), organizationId, userId, role)
        {
            JoinedAt = DateTime.UtcNow,
        };
    }

    public void ChangeRole(OrganizationRole newRole)
    {
        Role = newRole;
    }
}
