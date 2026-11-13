using System.Text;
using System.Text.Json;
using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.Infrastructure.Auth;

public class BrevoEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BrevoSettings _brevo;
    private readonly FrontendSettings _frontend;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<BrevoSettings> brevo,
        IOptions<FrontendSettings> frontend,
        ILogger<BrevoEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _brevo = brevo.Value;
        _frontend = frontend.Value;
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string toEmail, string toName, string rawToken, CancellationToken ct = default) =>
        SendAsync(
            "verification",
            toEmail,
            toName,
            EmailTemplates.Verification(toName, FrontendLink("/verify-email", rawToken)),
            ct);

    public Task SendOrganizationInvitationAsync(
        string toEmail,
        string organizationName,
        string inviterName,
        string roleName,
        string rawToken,
        CancellationToken ct = default) =>
        SendAsync(
            "invitation",
            toEmail,
            toEmail,
            EmailTemplates.OrganizationInvitation(
                toEmail, organizationName, inviterName, roleName, FrontendLink("/invitations/accept", rawToken)),
            ct);

    private string FrontendLink(string path, string rawToken) =>
        $"{_frontend.BaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(rawToken)}";

    private async Task SendAsync(string kind, string toEmail, string toName, EmailMessage message, CancellationToken ct)
    {
        var payload = new
        {
            sender = new { name = _brevo.SenderName, email = _brevo.SenderEmail },
            to = new[] { new { email = toEmail, name = toName } },
            subject = message.Subject,
            textContent = message.Text,
            htmlContent = message.Html,
        };

        var client = _httpClientFactory.CreateClient("Brevo");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await client.PostAsync("/v3/smtp/email", content, ct);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Brevo rejected the {Kind} email ({StatusCode}): {Body}", kind, (int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach Brevo to send the {Kind} email.", kind);
        }
    }
}
