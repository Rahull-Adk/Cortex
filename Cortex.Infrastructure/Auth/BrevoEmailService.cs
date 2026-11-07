using System.Text;
using System.Text.Json;
using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Options;

namespace Cortex.Infrastructure.Auth;

public class BrevoEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BrevoSettings _brevo;
    private readonly FrontendSettings _frontend;

    public BrevoEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<BrevoSettings> brevo,
        IOptions<FrontendSettings> frontend)
    {
        _httpClientFactory = httpClientFactory;
        _brevo = brevo.Value;
        _frontend = frontend.Value;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string toName, string rawToken, CancellationToken ct = default)
    {
        var link = $"{_frontend.BaseUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";

        var payload = new
        {
            sender = new { name = _brevo.SenderName, email = _brevo.SenderEmail },
            to = new[] { new { email = toEmail, name = toName } },
            subject = "Verify your email address",
            htmlContent = $"<p>Hi {toName},</p><p><a href=\"{link}\">Click here to verify your email</a></p><p>This link expires in 24 hours.</p>",
        };

        var client = _httpClientFactory.CreateClient("Brevo");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/v3/smtp/email", content, ct);
        response.EnsureSuccessStatusCode();
    }
}
