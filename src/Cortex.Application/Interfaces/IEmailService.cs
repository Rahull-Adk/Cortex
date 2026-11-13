namespace Cortex.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string toName, string rawToken, CancellationToken ct = default);

    Task SendOrganizationInvitationAsync(
        string toEmail, string organizationName, string inviterName, string rawToken, CancellationToken ct = default);
}
