using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default);
    void Add(RefreshToken token);
}
