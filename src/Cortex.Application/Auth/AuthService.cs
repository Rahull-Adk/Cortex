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
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IExternalIdentityTokenValidator _externalIdentityTokenValidator;
    private readonly IEmailService _emailService;

    public AuthService(
        IUserRepository users,
        IUserAccountRepository accounts,
        IEmailVerificationTokenRepository verificationTokens,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IExternalIdentityTokenValidator externalIdentityTokenValidator,
        IEmailService emailService)
    {
        _users = users;
        _accounts = accounts;
        _verificationTokens = verificationTokens;
        _refreshTokens = refreshTokens;
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
            return AuthErrors.EmailAlreadyRegistered;

        var user = User.Create(request.Name, request.Email);
        var account = UserAccount.CreateCredentials(user.Id, _passwordHasher.Hash(request.Password));
        var rawToken = SecureToken.Generate();
        var verificationToken = EmailVerificationToken.Create(user.Id, SecureToken.Hash(rawToken), TimeSpan.FromHours(24));

        _users.Add(user);
        _accounts.Add(account);
        _verificationTokens.Add(verificationToken);

        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawToken, ct);

        return MapToDto(user);
    }

    public async Task<Result<AuthTokensDto>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var user = await _users.FindByEmailAsync(emailNormalized, ct);
        if (user is null || !user.IsActive)
            return AuthErrors.InvalidCredentials;

        var account = await _accounts.FindCredentialsByUserIdAsync(user.Id, ct);
        if (account?.PasswordHash is null || !_passwordHasher.Verify(request.Password, account.PasswordHash))
            return AuthErrors.InvalidCredentials;

        if (!user.EmailVerifiedAt.HasValue)
            return AuthErrors.EmailNotVerified;

        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthTokensDto>> GoogleSessionAsync(GoogleSessionRequest request, CancellationToken ct = default)
    {
        var identity = await _externalIdentityTokenValidator.ValidateGoogleIdTokenAsync(request.IdToken, ct);
        if (identity is null)
            return AuthErrors.InvalidGoogleToken;

        const string provider = "google";
        var account = await _accounts.FindByProviderAsync(provider, identity.ProviderAccountId, ct);
        if (account is not null)
        {
            var existingUser = await _users.FindByIdAsync(account.UserId, ct)
                ?? throw new InvalidOperationException("User linked to account not found.");
            return await IssueTokensAsync(existingUser, ct);
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

        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthTokensDto>> VerifyEmailAsync(string rawToken, CancellationToken ct = default)
    {
        var token = await _verificationTokens.FindByTokenHashAsync(SecureToken.Hash(rawToken), ct);

        if (token is null || token.IsExpired)
            return AuthErrors.InvalidVerificationToken;

        var user = await _users.FindByIdAsync(token.UserId, ct)
            ?? throw new InvalidOperationException("User linked to verification token not found.");

        user.MarkEmailVerified();
        _verificationTokens.Remove(token);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(emailNormalized, ct);

        if (user is null)
            return AuthErrors.EmailNotRegistered;

        if (user.EmailVerifiedAt.HasValue)
            return AuthErrors.EmailAlreadyVerified;

        await _verificationTokens.DeleteByUserIdAsync(user.Id, ct);

        var rawToken = SecureToken.Generate();
        _verificationTokens.Add(EmailVerificationToken.Create(user.Id, SecureToken.Hash(rawToken), TimeSpan.FromHours(24)));

        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawToken, ct);
        return Result.Success();
    }

    public async Task<Result<AuthTokensDto>> RefreshAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return AuthErrors.InvalidRefreshToken;

        var token = await _refreshTokens.FindByTokenHashAsync(SecureToken.Hash(rawRefreshToken), ct);
        if (token is null)
            return AuthErrors.InvalidRefreshToken;

        // A rotated token being replayed means it may have been stolen.
        if (token.IsRevoked)
        {
            await RevokeFamilyAsync(token.FamilyId, ct);
            return AuthErrors.InvalidRefreshToken;
        }

        if (token.IsExpired)
            return AuthErrors.InvalidRefreshToken;

        var user = await _users.FindByIdAsync(token.UserId, ct);
        if (user is null || !user.IsActive || !user.EmailVerifiedAt.HasValue)
        {
            await RevokeFamilyAsync(token.FamilyId, ct);
            return AuthErrors.InvalidRefreshToken;
        }

        try
        {
            return await IssueTokensAsync(user, token.FamilyId, token, ct);
        }
        catch (ConcurrencyConflictException)
        {
            _unitOfWork.DiscardChanges();
            await RevokeFamilyAsync(token.FamilyId, ct);
            return AuthErrors.InvalidRefreshToken;
        }
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return;

        var token = await _refreshTokens.FindByTokenHashAsync(SecureToken.Hash(rawRefreshToken), ct);
        if (token is not null)
            await RevokeFamilyAsync(token.FamilyId, ct);
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId, ct);
        if (user is null)
            return AuthErrors.UserNotFound;

        return MapToDto(user);
    }

    private Task<AuthTokensDto> IssueTokensAsync(User user, CancellationToken ct) =>
        IssueTokensAsync(user, Guid.NewGuid(), null, ct);

    private async Task<AuthTokensDto> IssueTokensAsync(User user, Guid familyId, RefreshToken? replacedToken, CancellationToken ct)
    {
        var rawRefreshToken = SecureToken.Generate();
        var refreshToken = RefreshToken.Create(user.Id, familyId, SecureToken.Hash(rawRefreshToken), _tokenService.RefreshTokenLifetime);

        _refreshTokens.Add(refreshToken);
        replacedToken?.Revoke(refreshToken.Id);

        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user);

        return new AuthTokensDto(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshToken.ExpiresAt, MapToDto(user));
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        await _refreshTokens.RevokeFamilyAsync(familyId, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Name, user.Email, user.EmailVerifiedAt.HasValue, user.AvatarUrl);
}
