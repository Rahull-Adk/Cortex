using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(EmailVerificationToken token);
    void Remove(EmailVerificationToken token);
}
