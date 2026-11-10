namespace Cortex.Domain.Entities;

public class UserAccount
{
    private UserAccount() { }

    private UserAccount(Guid id, Guid userId, string provider, string providerAccountId)
    {
        Id = id;
        UserId = userId;
        Provider = provider;
        ProviderAccountId = providerAccountId;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = default!;
    public string ProviderAccountId { get; private set; } = default!;
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = default!;

    public static UserAccount CreateOAuth(Guid userId, string provider, string providerAccountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerAccountId);

        return new UserAccount(Guid.NewGuid(), userId, provider.ToLowerInvariant(), providerAccountId)
        {
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static UserAccount CreateCredentials(Guid userId, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new UserAccount(Guid.NewGuid(), userId, "credentials", userId.ToString())
        {
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }
}
