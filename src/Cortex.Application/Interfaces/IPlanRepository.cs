using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> ListActiveAsync(CancellationToken ct = default);
    Task<Plan?> FindByCodeAsync(string code, CancellationToken ct = default);
}
