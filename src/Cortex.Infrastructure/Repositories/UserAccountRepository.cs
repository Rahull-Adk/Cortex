using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Infrastructure.Repositories;

public class UserAccountRepository : IUserAccountRepository
{
    private readonly CortexDbContext _db;

    public UserAccountRepository(CortexDbContext db) => _db = db;

    public Task<UserAccount?> FindByProviderAsync(string provider, string providerAccountId, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(a => a.Provider == provider && a.ProviderAccountId == providerAccountId, ct);

    public Task<UserAccount?> FindCredentialsByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.UserAccounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == "credentials", ct);

    public void Add(UserAccount account) => _db.UserAccounts.Add(account);
}
