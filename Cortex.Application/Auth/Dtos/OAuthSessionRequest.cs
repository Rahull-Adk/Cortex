namespace Cortex.Application.Auth.Dtos;

public record OAuthSessionRequest(
    string Provider,
    string ProviderAccountId,
    string Email,
    string Name,
    string? AvatarUrl);
