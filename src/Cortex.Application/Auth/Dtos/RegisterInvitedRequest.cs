namespace Cortex.Application.Auth.Dtos;

public record RegisterInvitedRequest(string Token, string Name, string Password);
