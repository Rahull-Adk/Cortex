namespace Cortex.Infrastructure.Auth.Settings;

public class FrontendSettings
{
    public string BaseUrl { get; set; } = default!;

    // Pages the emailed links open. The token is appended as ?token=...
    public string VerifyEmailPath { get; set; } = "/auth/verify-email";
    public string InvitationAcceptPath { get; set; } = "/invitations/accept";

    // Absolute URL of a PNG logo for emails. Optional.
    public string? EmailLogoUrl { get; set; }
}
