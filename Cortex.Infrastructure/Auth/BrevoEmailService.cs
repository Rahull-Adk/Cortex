using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
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

    public async Task SendVerificationEmailAsync(string toEmail, string toName, string rawToken, CancellationToken ct = default)
    {
        var link = $"{_frontend.BaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        var safeName = HtmlEncoder.Default.Encode(toName);
        var safeLink = HtmlEncoder.Default.Encode(link);

        var payload = new
        {
            sender = new { name = _brevo.SenderName, email = _brevo.SenderEmail },
            to = new[] { new { email = toEmail, name = toName } },
            subject = "Confirm your Cortex email address",
            textContent = $"Hello {toName},\n\nThanks for creating a Cortex account. Confirm your email address to finish setting up your account:\n{link}\n\nThis link expires in 24 hours. If you didn't create a Cortex account, you can ignore this email.",
            htmlContent = $$"""
                <!doctype html>
                <html lang="en">
                  <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <meta name="color-scheme" content="light">
                    <title>Confirm your email</title>
                  </head>
                  <body style="margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#172033;">
                    <div style="display:none;max-height:0;overflow:hidden;opacity:0;">Confirm your email to finish setting up your Cortex account.</div>
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f4f6f8;padding:40px 16px;">
                      <tr><td align="center">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#ffffff;border:1px solid #e5e9f0;border-radius:12px;">
                          <tr><td style="padding:32px 40px 12px;font-size:22px;font-weight:700;letter-spacing:-0.3px;color:#172033;">Cortex</td></tr>
                          <tr><td style="padding:16px 40px 36px;">
                            <h1 style="margin:0 0 20px;font-size:24px;line-height:1.3;color:#172033;">Confirm your email address</h1>
                            <p style="margin:0 0 16px;font-size:16px;line-height:1.6;">Hello {{safeName}},</p>
                            <p style="margin:0 0 24px;font-size:16px;line-height:1.6;color:#465166;">Thanks for creating a Cortex account. Confirm your email address to finish setting up your account.</p>
                            <p style="margin:0 0 28px;"><a href="{{safeLink}}" style="display:inline-block;padding:14px 22px;border-radius:7px;background:#315efb;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;">Confirm email address</a></p>
                            <p style="margin:0 0 10px;font-size:14px;line-height:1.6;color:#465166;">This link expires in 24 hours.</p>
                            <p style="margin:0 0 22px;font-size:14px;line-height:1.6;color:#465166;">If the button doesn't work, copy and paste this address into your browser:</p>
                            <p style="margin:0 0 24px;word-break:break-all;font-size:13px;line-height:1.6;"><a href="{{safeLink}}" style="color:#315efb;">{{safeLink}}</a></p>
                            <p style="margin:0;border-top:1px solid #e9edf3;padding-top:20px;font-size:13px;line-height:1.6;color:#6b7485;">If you didn't create a Cortex account, you can safely ignore this email.</p>
                          </td></tr>
                        </table>
                        <p style="margin:20px 0 0;font-size:12px;color:#8790a0;">Cortex · Conversational commerce for your business</p>
                      </td></tr>
                    </table>
                  </body>
                </html>
                """,
        };

        var client = _httpClientFactory.CreateClient("Brevo");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await client.PostAsync("/v3/smtp/email", content, ct);
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Brevo rejected the verification email ({StatusCode}): {Body}", (int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach Brevo to send the verification email.");
        }
    }
}
