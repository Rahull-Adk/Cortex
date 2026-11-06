namespace Cortex.Domain.Entities;

public class User
{
    private readonly List<UserAccount> _accounts = [];
    private readonly List<OrganizationMembership> _memberships = [];

    private User() { }

    private User(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<UserAccount> Accounts => _accounts.AsReadOnly();
    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    public static User Create(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var now = DateTime.UtcNow;

        return new User(Guid.NewGuid(), name.Trim(), email.Trim().ToLowerInvariant())
        {
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void UpdateProfile(string name, string? avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEmailVerified()
    {
        if (EmailVerifiedAt.HasValue) return;
        EmailVerifiedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
