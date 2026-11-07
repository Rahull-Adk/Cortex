using System.Security.Cryptography;
using System.Text;
using Cortex.Application.Auth.Dtos;
using Cortex.Application.Common;
using Cortex.Application.Interfaces;
using Cortex.Domain.Entities;

namespace Cortex.Application.Auth;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IUserAccountRepository _accounts;
    private readonly IEmailVerificationTokenRepository _verificationTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IExternalIdentityTokenValidator _externalIdentityTokenValidator;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository users,
        IUserAccountRepository accounts,
        IEmailVerificationTokenRepository verificationTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IExternalIdentityTokenValidator externalIdentityTokenValidator,
        IEmailService emailService)
    {
        _users = users;
        _accounts = accounts;
        _verificationTokens = verificationTokens;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _externalIdentityTokenValidator = externalIdentityTokenValidator;
        _emailService = emailService;
    }

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(emailNormalized, ct))
            return Result.Failure<UserDto>(AuthErrors.EmailAlreadyRegistered, "This email is already registered.");

        var user = User.Create(request.Name, request.Email);
        var account = UserAccount.CreateCredentials(user.Id, _passwordHasher.Hash(request.Password));
        var rawToken = GenerateRawToken();
        var verificationToken = EmailVerificationToken.Create(user.Id, HashToken(rawToken), TimeSpan.FromHours(24));

        _users.Add(user);
        _accounts.Add(account);
        _verificationTokens.Add(verificationToken);

        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawToken, ct);

        return Result.Success(MapToDto(user));
    }

    public async Task<Result<AuthTokensDto>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var user = await _users.FindByEmailAsync(emailNormalized, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<AuthTokensDto>(AuthErrors.InvalidCredentials, "Invalid email or password.");

        var account = await _accounts.FindCredentialsByUserIdAsync(user.Id, ct);
                if (account?.PasswordHash is null || !_passwordHasher.Verify(request.Password, account.PasswordHash))
                    return Result.Failure<AuthTokensDto>(AuthErrors.InvalidCredentials, "Invalid email or password.");

        if (!user.EmailVerifiedAt.HasValue)
            return Result.Failure<AuthTokensDto>(AuthErrors.EmailNotVerified, "Verify your email address before signing in.");

        

        return Result.Success(BuildTokens(user));
    }

    public async Task<Result<AuthTokensDto>> GoogleSessionAsync(GoogleSessionRequest request, CancellationToken ct = default)
    {
        var identity = await _externalIdentityTokenValidator.ValidateGoogleIdTokenAsync(request.IdToken, ct);
        if (identity is null)
            return Result.Failure<AuthTokensDto>(AuthErrors.InvalidToken, "The Google identity token is invalid or expired.");

        const string provider = "google";
        var account = await _accounts.FindByProviderAsync(provider, identity.ProviderAccountId, ct);
        if (account is not null)
        {
            var existingUser = await _users.FindByIdAsync(account.UserId, ct)
                ?? throw new InvalidOperationException("User linked to account not found.");
            return Result.Success(BuildTokens(existingUser));
        }

        var emailNormalized = identity.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(emailNormalized, ct);

        if (user is null)
        {
            user = User.Create(identity.Name, identity.Email);
            _users.Add(user);
        }

        user.MarkEmailVerified();
        user.UpdateProfile(identity.Name, identity.AvatarUrl);

        _accounts.Add(UserAccount.CreateOAuth(user.Id, provider, identity.ProviderAccountId));

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(BuildTokens(user));
    }

    public async Task<Result<AuthTokensDto>> VerifyEmailAsync(string rawToken, CancellationToken ct = default)
    {
        var token = await _verificationTokens.FindByTokenHashAsync(HashToken(rawToken), ct);

        if (token is null || token.IsExpired)
            return Result.Failure<AuthTokensDto>(AuthErrors.InvalidToken, "Invalid or expired verification token.");

        var user = await _users.FindByIdAsync(token.UserId, ct)
            ?? throw new InvalidOperationException("User linked to verification token not found.");

        user.MarkEmailVerified();
        _verificationTokens.Remove(token);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(BuildTokens(user));
    }

    public async Task<Result> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(emailNormalized, ct);

        if (user is null || user.EmailVerifiedAt.HasValue)
            return Result.Success();

        await _verificationTokens.DeleteByUserIdAsync(user.Id, ct);

        var rawToken = GenerateRawToken();
        _verificationTokens.Add(EmailVerificationToken.Create(user.Id, HashToken(rawToken), TimeSpan.FromHours(24)));

        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawToken, ct);

        return Result.Success();
    }

    public async Task<Result<AuthTokensDto>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var userId = _tokenService.ValidateRefreshToken(request.RefreshToken);
        if (userId is null)
            return Result.Failure<AuthTokensDto>(AuthErrors.InvalidToken, "Invalid or expired refresh token.");

        var user = await _users.FindByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive || !user.EmailVerifiedAt.HasValue)
            return Result.Failure<AuthTokensDto>(AuthErrors.InvalidToken, "Invalid or expired refresh token.");

        return Result.Success(BuildTokens(user));
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        return user is null
            ? Result.Failure<UserDto>(AuthErrors.InvalidToken, "User not found.")
            : Result.Success(MapToDto(user));
    }

    private AuthTokensDto BuildTokens(User user) =>
        new(_tokenService.GenerateAccessToken(user), _tokenService.GenerateRefreshToken(user), MapToDto(user));

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Name, user.Email, user.EmailVerifiedAt.HasValue, user.AvatarUrl);

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
