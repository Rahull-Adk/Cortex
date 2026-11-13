using System.Text;
using System.Text.Encodings.Web;

namespace Cortex.Infrastructure.Auth;

public sealed record EmailMessage(string Subject, string Text, string Html);

// LogoUrl must point at a PNG: Gmail and Outlook do not render SVG. Without it the header is the wordmark alone.
public sealed record EmailBranding(string SiteUrl, string? LogoUrl = null);

public static class EmailTemplates
{
    // Colours and type mirror the frontend so emails read as the same product.
    private const string Ink = "#12172a";
    private const string Teal = "#00ab8f";
    private const string TealDark = "#007a66";
    private const string Mist = "#f4f6f8";
    private const string Body = "#414a5e";
    private const string Muted = "#69728a";
    private const string Line = "#e3e7ee";

    private const string Sans = "Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    private const string Serif = "Newsreader,Georgia,'Times New Roman',serif";
    private const string Mono = "ui-monospace,SFMono-Regular,Menlo,Consolas,'Liberation Mono',monospace";

    private const string Tagline = "AI assistant for your website";

    public static EmailMessage Verification(EmailBranding branding, string name, string link)
    {
        var text = $"""
            Hi {name},

            Welcome to Cortex. Confirm your email address to activate your account:

            {link}

            This link expires in 24 hours and can be used once.

            If you didn't create a Cortex account, you can ignore this email and no account will be activated.

            Cortex
            {Tagline}
            {branding.SiteUrl}
            """;

        var html = Layout(
            branding,
            preheader: "Confirm your email address to activate your Cortex account.",
            eyebrow: "Account setup",
            heading: "Confirm your email address",
            paragraphs:
            [
                $"Hi {Encode(name)},",
                "Welcome to Cortex. Confirm your email address to activate your account and start setting up your workspace.",
            ],
            details: [],
            buttonText: "Confirm email address",
            link: link,
            expiry: "This link expires in 24 hours and can be used once.",
            securityNote: "If you didn't create a Cortex account, you can ignore this email and no account will be activated.",
            reason: "You received this email because this address was used to sign up for Cortex.");

        return new EmailMessage("Confirm your email address", text, html);
    }

    public static EmailMessage OrganizationInvitation(
        EmailBranding branding,
        string toEmail,
        string organizationName,
        string inviterName,
        string roleName,
        string link)
    {
        var text = $"""
            Hello,

            {inviterName} has invited you to join {organizationName} on Cortex.

            Business:    {organizationName}
            Invited by:  {inviterName}
            Your role:   {roleName}

            Accept the invitation:

            {link}

            This invitation expires in 7 days and only works for {toEmail}. You will join only after you accept.

            If you weren't expecting this invitation, you can ignore this email.

            Cortex
            {Tagline}
            {branding.SiteUrl}
            """;

        var html = Layout(
            branding,
            preheader: $"{Encode(inviterName)} has invited you to join {Encode(organizationName)} on Cortex.",
            eyebrow: "Team invitation",
            heading: $"Join {Encode(organizationName)} on Cortex",
            paragraphs:
            [
                "Hello,",
                $"<strong style=\"color:{Ink};font-weight:600;\">{Encode(inviterName)}</strong> has invited you to work with them in the <strong style=\"color:{Ink};font-weight:600;\">{Encode(organizationName)}</strong> workspace.",
            ],
            details:
            [
                ("Business", Encode(organizationName)),
                ("Invited by", Encode(inviterName)),
                ("Your role", Encode(roleName)),
            ],
            buttonText: "Accept invitation",
            link: link,
            expiry: $"This invitation expires in 7 days and only works for {Encode(toEmail)}. You will join only after you accept.",
            securityNote: "If you weren't expecting this invitation, you can ignore this email. Nothing happens unless you accept.",
            reason: $"You received this email because a member of {Encode(organizationName)} invited this address to Cortex.");

        return new EmailMessage($"{inviterName} invited you to join {organizationName} on Cortex", text, html);
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    // Everything except link and branding must already be HTML-encoded. Built with tables
    // and inline styles because email clients ignore most modern CSS.
    private static string Layout(
        EmailBranding branding,
        string preheader,
        string eyebrow,
        string heading,
        IReadOnlyList<string> paragraphs,
        IReadOnlyList<(string Label, string Value)> details,
        string buttonText,
        string link,
        string expiry,
        string securityNote,
        string reason)
    {
        var safeLink = Encode(link);
        var safeSiteUrl = Encode(branding.SiteUrl);
        var siteLabel = Encode(DisplayHost(branding.SiteUrl));

        var logoCell = string.IsNullOrWhiteSpace(branding.LogoUrl)
            ? string.Empty
            : $"""<td width="28" valign="middle" style="width:28px;padding-right:10px;"><img src="{Encode(branding.LogoUrl)}" width="28" height="28" alt="" style="display:block;width:28px;height:28px;border:0;outline:none;"></td>""";

        var body = new StringBuilder();
        foreach (var paragraph in paragraphs)
            body.Append($"""<p style="margin:0 0 16px;font-family:{Sans};font-size:16px;line-height:26px;color:{Body};">{paragraph}</p>""");

        var detailsBlock = string.Empty;
        if (details.Count > 0)
        {
            var rows = new StringBuilder();
            for (var i = 0; i < details.Count; i++)
            {
                var border = i == details.Count - 1 ? string.Empty : $"border-bottom:1px solid {Line};";
                rows.Append($"""
                    <tr>
                      <td width="110" style="padding:12px 16px;{border}font-family:{Mono};font-size:11px;line-height:20px;letter-spacing:0.08em;text-transform:uppercase;color:{Muted};white-space:nowrap;">{details[i].Label}</td>
                      <td style="padding:12px 16px;{border}font-family:{Sans};font-size:15px;line-height:20px;font-weight:600;color:{Ink};">{details[i].Value}</td>
                    </tr>
                    """);
            }

            detailsBlock = $"""
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="{Mist}" style="margin:8px 0 24px;background-color:{Mist};border:1px solid {Line};border-radius:8px;">
                  {rows}
                </table>
                """;
        }

        return $$"""
            <!doctype html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="x-apple-disable-message-reformatting">
                <meta name="color-scheme" content="light">
                <meta name="supported-color-schemes" content="light">
                <title>{{heading}}</title>
                <style>
                  body { margin:0; padding:0; width:100% !important; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; }
                  table { border-collapse:separate; }
                  @media only screen and (max-width:620px) {
                    .outer { padding:24px 12px !important; }
                    .card-pad { padding:28px 24px !important; }
                    .heading { font-size:26px !important; line-height:32px !important; }
                  }
                </style>
              </head>
              <body style="margin:0;padding:0;background-color:{{Mist}};font-family:{{Sans}};color:{{Ink}};">
                <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;font-size:1px;line-height:1px;color:{{Mist}};">{{preheader}}&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;</div>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="{{Mist}}" style="background-color:{{Mist}};">
                  <tr>
                    <td class="outer" align="center" style="padding:40px 16px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;">
                        <tr>
                          <td style="padding:0 4px 24px;">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                {{logoCell}}
                                <td valign="middle" style="font-family:{{Sans}};font-size:19px;line-height:28px;font-weight:600;letter-spacing:-0.2px;"><a href="{{safeSiteUrl}}" target="_blank" style="color:{{Ink}};text-decoration:none;">Cortex</a></td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td class="card-pad" bgcolor="#ffffff" style="padding:40px 40px 32px;background-color:#ffffff;border:1px solid {{Line}};border-radius:12px;">
                            <p style="margin:0 0 12px;font-family:{{Mono}};font-size:11px;line-height:16px;letter-spacing:0.12em;text-transform:uppercase;color:{{TealDark}};">{{eyebrow}}</p>
                            <h1 class="heading" style="margin:0 0 20px;font-family:{{Serif}};font-size:30px;line-height:36px;font-weight:500;letter-spacing:-0.4px;color:{{Ink}};">{{heading}}</h1>
                            {{body}}
                            {{detailsBlock}}
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="margin:8px 0 20px;">
                              <tr>
                                <td align="center" bgcolor="{{Teal}}" style="border-radius:8px;background-color:{{Teal}};">
                                  <!--[if mso]>
                                  <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{{safeLink}}" style="height:48px;v-text-anchor:middle;width:240px;" arcsize="17%" stroke="f" fillcolor="{{Teal}}">
                                    <w:anchorlock/>
                                    <center style="color:{{Ink}};font-family:Arial,sans-serif;font-size:16px;font-weight:bold;">{{buttonText}}</center>
                                  </v:roundrect>
                                  <![endif]-->
                                  <!--[if !mso]><!-- -->
                                  <a href="{{safeLink}}" target="_blank" style="display:inline-block;padding:14px 28px;border-radius:8px;background-color:{{Teal}};font-family:{{Sans}};font-size:16px;line-height:20px;font-weight:600;color:{{Ink}};text-decoration:none;">{{buttonText}}</a>
                                  <!--<![endif]-->
                                </td>
                              </tr>
                            </table>
                            <p style="margin:0 0 28px;font-family:{{Sans}};font-size:14px;line-height:22px;color:{{Muted}};">{{expiry}}</p>
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td style="border-top:1px solid {{Line}};padding-top:24px;font-family:{{Sans}};">
                                  <p style="margin:0 0 6px;font-size:13px;line-height:20px;color:{{Muted}};">Button not working? Copy and paste this link into your browser:</p>
                                  <p style="margin:0 0 20px;font-size:13px;line-height:20px;word-break:break-all;"><a href="{{safeLink}}" target="_blank" style="color:{{TealDark}};text-decoration:underline;">{{safeLink}}</a></p>
                                  <p style="margin:0;font-size:13px;line-height:20px;color:{{Muted}};">{{securityNote}}</p>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td align="center" style="padding:28px 24px 0;font-family:{{Sans}};">
                            <p style="margin:0 0 4px;font-size:13px;line-height:20px;font-weight:600;color:{{Ink}};">Cortex</p>
                            <p style="margin:0 0 4px;font-size:12px;line-height:18px;color:{{Muted}};">{{Tagline}}</p>
                            <p style="margin:0 0 16px;font-size:12px;line-height:18px;"><a href="{{safeSiteUrl}}" target="_blank" style="color:{{TealDark}};text-decoration:underline;">{{siteLabel}}</a></p>
                            <p style="margin:0 0 4px;font-size:12px;line-height:18px;color:{{Muted}};">{{reason}}</p>
                            <p style="margin:0;font-size:12px;line-height:18px;color:{{Muted}};">This is an automated message, so replies are not monitored.</p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    private static string DisplayHost(string siteUrl) =>
        Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ? uri.Host : siteUrl;
}
