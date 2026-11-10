using Cortex.Domain.Entities;

namespace Cortex.Application.Interfaces;

public interface ITokenService
{
    AccessToken GenerateAccessToken(User user);
    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);
