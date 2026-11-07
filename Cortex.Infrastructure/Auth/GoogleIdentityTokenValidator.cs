using Cortex.Application.Auth.Dtos;
using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Auth.Settings;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Cortex.Infrastructure.Auth;

public sealed class GoogleIdentityTokenValidator(IOptions<GoogleAuthSettings> settings) : IExternalIdentityTokenValidator
{
    public async Task<ExternalIdentity?> ValidateGoogleIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.ClientId))
            throw new InvalidOperationException("GoogleAuth:ClientId must be configured to enable Google sign-in.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [settings.Value.ClientId],
            });

            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(payload.Email) ||
                !payload.EmailVerified ||
                string.IsNullOrWhiteSpace(payload.Name))
                return null;

            return new ExternalIdentity(payload.Subject, payload.Email, payload.Name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
