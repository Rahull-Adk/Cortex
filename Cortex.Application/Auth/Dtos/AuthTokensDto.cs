namespace Cortex.Application.Auth.Dtos;

public record AuthTokensDto(string AccessToken, string RefreshToken, UserDto User);
