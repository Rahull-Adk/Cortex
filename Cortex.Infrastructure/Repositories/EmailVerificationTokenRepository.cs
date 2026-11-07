using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly CortexDbContext _db;

    public EmailVerificationTokenRepository(CortexDbContext db) => _db = db;

    public Task<EmailVerificationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.EmailVerificationTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        _db.EmailVerificationTokens.RemoveRange(tokens);
    }

    public void Add(EmailVerificationToken token) => _db.EmailVerificationTokens.Add(token);

    public void Remove(EmailVerificationToken token) => _db.EmailVerificationTokens.Remove(token);
}
