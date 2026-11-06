namespace Cortex.Domain.Entities;

public class Organization
{
    private readonly List<OrganizationMembership> _memberships = [];

    private Organization() { }

    private Organization(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? LogoUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    public static Organization Create(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var now = DateTime.UtcNow;

        return new Organization(Guid.NewGuid(), name.Trim(), slug.Trim().ToLowerInvariant())
        {
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void UpdateDetails(string name, string? logoUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        LogoUrl = logoUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
