namespace Cortex.Domain.Entities;

public class EmailVerificationToken
{
    private EmailVerificationToken() { }

    private EmailVerificationToken(Guid id, Guid userId, string tokenHash, DateTime expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = default!;

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public static EmailVerificationToken Create(Guid userId, string tokenHash, TimeSpan validity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTime.UtcNow;

        return new EmailVerificationToken(Guid.NewGuid(), userId, tokenHash, now.Add(validity))
        {
            CreatedAt = now,
        };
    }
}
