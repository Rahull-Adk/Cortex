using System.Security.Cryptography;
using System.Text;
using Cortex.Domain.Entities;

namespace Cortex.Application.Tests.Support;

internal static class TestData
{
    public static User UnverifiedUser(string email = "owner@example.com", string name = "Owner") =>
        User.Create(name, email);

    public static User VerifiedUser(string email = "owner@example.com", string name = "Owner")
    {
        var user = User.Create(name, email);
        user.MarkEmailVerified();
        return user;
    }

    public static User InactiveUser(string email = "owner@example.com", string name = "Owner")
    {
        var user = VerifiedUser(email, name);
        user.Deactivate();
        return user;
    }

    public static string Sha256(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    public static RefreshToken RefreshTokenFor(User user, string rawToken) =>
        RefreshToken.Create(user.Id, Guid.NewGuid(), Sha256(rawToken), TimeSpan.FromDays(7));

    // RefreshToken.Create rejects non-positive lifetimes and there is no clock to move.
    public static RefreshToken ExpiredRefreshTokenFor(User user, string rawToken)
    {
        var token = RefreshTokenFor(user, rawToken);
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.ExpiresAt))!
            .SetValue(token, DateTime.UtcNow.AddMinutes(-1));
        return token;
    }
}
