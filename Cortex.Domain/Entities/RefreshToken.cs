namespace Cortex.Domain.Entities;

public class RefreshToken
{
    private RefreshToken() { }

    private RefreshToken(Guid id, Guid userId, Guid familyId, string tokenHash, DateTime expiresAt)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public User User { get; private set; } = default!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public static RefreshToken Create(Guid userId, Guid familyId, string tokenHash, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Refresh token lifetime must be positive.");

        var now = DateTime.UtcNow;

        return new RefreshToken(Guid.NewGuid(), userId, familyId, tokenHash, now.Add(lifetime))
        {
            CreatedAt = now,
        };
    }

    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (IsRevoked) return;
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
