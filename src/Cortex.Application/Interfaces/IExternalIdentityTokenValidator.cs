using Cortex.Application.Auth.Dtos;

namespace Cortex.Application.Interfaces;

public interface IExternalIdentityTokenValidator
{
    Task<ExternalIdentity?> ValidateGoogleIdTokenAsync(string idToken, CancellationToken ct = default);
}
