namespace Cortex.Application.Auth.Dtos;

public record ExternalIdentity(string ProviderAccountId, string Email, string Name, string? AvatarUrl);
