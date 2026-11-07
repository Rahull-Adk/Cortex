namespace Cortex.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string toName, string rawToken, CancellationToken ct = default);
}
