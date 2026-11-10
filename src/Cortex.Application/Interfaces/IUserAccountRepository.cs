using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IUserAccountRepository
{
    Task<UserAccount?> FindByProviderAsync(string provider, string providerAccountId, CancellationToken ct = default);
    Task<UserAccount?> FindCredentialsByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(UserAccount account);
}
