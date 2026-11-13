using Cortex.Domain.Enums;

namespace Cortex.Domain.Entities;

// A row exists only while the invitation is pending. Accepting or revoking deletes it.
public class OrganizationInvitation
{
    private OrganizationInvitation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = default!;
    public OrganizationRole Role { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid InvitedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public Organization Organization { get; private set; } = default!;
    public User InvitedBy { get; private set; } = default!;

    public static OrganizationInvitation Create(
        Guid organizationId,
        string email,
        OrganizationRole role,
        string tokenHash,
        Guid invitedByUserId,
        TimeSpan validity)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("An invitation requires an organization.", nameof(organizationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email.Trim().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow,
        };

        invitation.Renew(role, tokenHash, invitedByUserId, validity);
        return invitation;
    }

    // Inviting the same email again replaces the link, so the older email stops working.
    public void Renew(OrganizationRole role, string tokenHash, Guid invitedByUserId, TimeSpan validity)
    {
        if (role is not (OrganizationRole.Admin or OrganizationRole.Member))
            throw new ArgumentException("Only Admin or Member can be invited.", nameof(role));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (invitedByUserId == Guid.Empty)
            throw new ArgumentException("An invitation requires an inviter.", nameof(invitedByUserId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(validity, TimeSpan.Zero);

        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        ExpiresAt = DateTime.UtcNow.Add(validity);
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
}
