using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken(User user);
    Guid? ValidateRefreshToken(string token);
}
