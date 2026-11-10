namespace Cortex.Application.Auth.Dtos;

public record UserDto(Guid Id, string Name, string Email, bool EmailVerified, string? AvatarUrl);
