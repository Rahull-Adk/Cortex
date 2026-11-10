using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Cortex.Domain.Entities;
using Cortex.Infrastructure.Auth;
using Cortex.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cortex.Infrastructure.Tests.Auth;

public class JwtTokenServiceTests
{
    private const string TestSecret = "unit-test-signing-key-not-a-real-secret-0123456789";

    private static readonly JwtSettings Settings = new()
    {
        Secret = TestSecret,
        Issuer = "cortex-api-tests",
        Audience = "cortex-frontend-tests",
        AccessTokenExpiryMinutes = 15,
        RefreshTokenExpiryDays = 7,
    };

    private static JwtTokenService CreateService(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? Settings));

    private static User VerifiedUser()
    {
        var user = User.Create("Aye Chan", "aye@example.com");
        user.MarkEmailVerified();
        return user;
    }

    private static TokenValidationParameters ValidationParameters(
        string secret = TestSecret,
        string issuer = "cortex-api-tests",
        string audience = "cortex-frontend-tests") => new()
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };

    private static JwtSecurityToken Validate(string token, TokenValidationParameters parameters)
    {
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, parameters, out var validated);
        return (JwtSecurityToken)validated;
    }

    private static string Claim(JwtSecurityToken token, string type) => token.Claims.Single(c => c.Type == type).Value;

    [Fact]
    public void GenerateAccessToken_ConfiguredKeyIssuerAndAudience_ProducesTokenThatValidates()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());

        var token = Validate(access.Token, ValidationParameters());

        Assert.Equal("cortex-api-tests", token.Issuer);
        Assert.Contains("cortex-frontend-tests", token.Audiences);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.SignatureAlgorithm);
    }

    [Fact]
    public void GenerateAccessToken_ValidatedWithDifferentKey_IsRejected()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());
        var parameters = ValidationParameters(secret: "a-different-signing-key-that-is-long-enough-000000");

        Assert.ThrowsAny<SecurityTokenException>(() => Validate(access.Token, parameters));
    }

    [Fact]
    public void GenerateAccessToken_ValidatedForDifferentAudience_IsRejected()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(access.Token, ValidationParameters(audience: "some-other-app")));
    }

    [Fact]
    public void GenerateAccessToken_ValidatedForDifferentIssuer_IsRejected()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(access.Token, ValidationParameters(issuer: "some-other-issuer")));
    }

    [Fact]
    public void GenerateAccessToken_TamperedPayload_IsRejected()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());
        var parts = access.Token.Split('.');
        var otherPayload = CreateService().GenerateAccessToken(User.Create("Mallory", "mallory@example.com"))
            .Token.Split('.')[1];
        var tampered = $"{parts[0]}.{otherPayload}.{parts[2]}";

        Assert.ThrowsAny<SecurityTokenException>(() => Validate(tampered, ValidationParameters()));
    }

    [Fact]
    public void GenerateAccessToken_User_CarriesIdentityClaims()
    {
        var user = VerifiedUser();

        var access = CreateService().GenerateAccessToken(user);

        var token = Validate(access.Token, ValidationParameters());
        Assert.Equal(user.Id.ToString(), Claim(token, JwtRegisteredClaimNames.Sub));
        Assert.Equal("aye@example.com", Claim(token, JwtRegisteredClaimNames.Email));
        Assert.Equal("Aye Chan", Claim(token, JwtRegisteredClaimNames.Name));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void GenerateAccessToken_EmailVerifiedClaim_ReflectsUserState(bool verified, string expected)
    {
        var user = User.Create("Aye Chan", "aye@example.com");
        if (verified)
            user.MarkEmailVerified();

        var access = CreateService().GenerateAccessToken(user);

        var token = Validate(access.Token, ValidationParameters());
        Assert.Equal(expected, Claim(token, "email_verified"));
    }

    [Fact]
    public void GenerateAccessToken_AnyUser_ContainsNoRoleOrBusinessClaims()
    {
        var access = CreateService().GenerateAccessToken(VerifiedUser());

        var token = Validate(access.Token, ValidationParameters());
        string[] allowed =
        [
            JwtRegisteredClaimNames.Sub,
            JwtRegisteredClaimNames.Email,
            JwtRegisteredClaimNames.Name,
            "email_verified",
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Aud,
        ];
        Assert.Empty(token.Claims.Select(c => c.Type).Except(allowed));
    }

    [Fact]
    public void GenerateAccessToken_ConfiguredLifetime_ExpiresAfterThatManyMinutes()
    {
        var before = DateTime.UtcNow;

        var access = CreateService().GenerateAccessToken(VerifiedUser());

        var after = DateTime.UtcNow;
        var token = Validate(access.Token, ValidationParameters());
        Assert.InRange(access.ExpiresAt, before.AddMinutes(15), after.AddMinutes(15));
        Assert.InRange(token.ValidTo, access.ExpiresAt.AddSeconds(-1), access.ExpiresAt);
    }

    [Fact]
    public void GenerateAccessToken_ShorterConfiguredLifetime_IsHonoured()
    {
        var settings = new JwtSettings
        {
            Secret = TestSecret,
            Issuer = Settings.Issuer,
            Audience = Settings.Audience,
            AccessTokenExpiryMinutes = 1,
        };
        var before = DateTime.UtcNow;

        var access = CreateService(settings).GenerateAccessToken(VerifiedUser());

        Assert.InRange(access.ExpiresAt, before.AddMinutes(1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void GenerateAccessToken_AlreadyExpiredLifetime_FailsLifetimeValidation()
    {
        var settings = new JwtSettings
        {
            Secret = TestSecret,
            Issuer = Settings.Issuer,
            Audience = Settings.Audience,
            AccessTokenExpiryMinutes = -5,
        };

        var access = CreateService(settings).GenerateAccessToken(VerifiedUser());

        Assert.Throws<SecurityTokenExpiredException>(() => Validate(access.Token, ValidationParameters()));
    }

    [Fact]
    public void GenerateAccessToken_CalledTwiceForSameUser_ProducesDistinctTokenIds()
    {
        var service = CreateService();
        var user = VerifiedUser();

        var first = service.GenerateAccessToken(user);
        var second = service.GenerateAccessToken(user);

        var firstId = Claim(Validate(first.Token, ValidationParameters()), JwtRegisteredClaimNames.Jti);
        var secondId = Claim(Validate(second.Token, ValidationParameters()), JwtRegisteredClaimNames.Jti);
        Assert.NotEqual(firstId, secondId);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public void RefreshTokenLifetime_ConfiguredDays_IsReturnedAsTimeSpan(int days)
    {
        var settings = new JwtSettings
        {
            Secret = TestSecret,
            Issuer = Settings.Issuer,
            Audience = Settings.Audience,
            RefreshTokenExpiryDays = days,
        };

        var lifetime = CreateService(settings).RefreshTokenLifetime;

        Assert.Equal(TimeSpan.FromDays(days), lifetime);
    }
}
