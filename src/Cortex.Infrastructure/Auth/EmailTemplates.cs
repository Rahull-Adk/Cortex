using System.Text;
using System.Text.Encodings.Web;

namespace Cortex.Infrastructure.Auth;

public sealed record EmailMessage(string Subject, string Text, string Html);

public static class EmailTemplates
{
    private const string Brand = "#315efb";
    private const string Font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    public static EmailMessage Verification(string name, string link)
    {
        var text = $"""
            Hi {name},

            Welcome to Cortex. Confirm your email address to activate your account:

            {link}

            This link expires in 24 hours and can be used once.

            If you didn't create a Cortex account, you can ignore this email and no account will be activated.

            Cortex
            Conversational commerce for your business
            """;

        var html = Layout(
            preheader: "Confirm your email address to activate your Cortex account.",
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
        string toEmail, string organizationName, string inviterName, string roleName, string link)
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
            Conversational commerce for your business
            """;

        var html = Layout(
            preheader: $"{Encode(inviterName)} has invited you to join {Encode(organizationName)} on Cortex.",
            heading: $"You're invited to join {Encode(organizationName)}",
            paragraphs:
            [
                "Hello,",
                $"<strong style=\"color:#111827;\">{Encode(inviterName)}</strong> has invited you to work with them in the <strong style=\"color:#111827;\">{Encode(organizationName)}</strong> workspace on Cortex.",
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

    // Everything except link must already be HTML-encoded. Built with tables and inline
    // styles because email clients ignore most modern CSS.
    private static string Layout(
        string preheader,
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

        var body = new StringBuilder();
        foreach (var paragraph in paragraphs)
            body.Append($"""<p style="margin:0 0 16px;font-size:16px;line-height:26px;color:#374151;">{paragraph}</p>""");

        var detailsBlock = string.Empty;
        if (details.Count > 0)
        {
            var rows = new StringBuilder();
            for (var i = 0; i < details.Count; i++)
            {
                var border = i == details.Count - 1 ? string.Empty : "border-bottom:1px solid #e5e7eb;";
                rows.Append($"""
                    <tr>
                      <td style="padding:12px 16px;{border}font-size:13px;line-height:20px;color:#6b7280;white-space:nowrap;" width="110">{details[i].Label}</td>
                      <td style="padding:12px 16px;{border}font-size:15px;line-height:20px;color:#111827;font-weight:600;">{details[i].Value}</td>
                    </tr>
                    """);
            }

            detailsBlock = $"""
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f9fafb" style="margin:8px 0 24px;background-color:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;">
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
                  a { color:{{Brand}}; }
                  @media only screen and (max-width:620px) {
                    .outer { padding:24px 12px !important; }
                    .card-pad { padding:28px 24px !important; }
                    .heading { font-size:22px !important; line-height:30px !important; }
                  }
                </style>
              </head>
              <body style="margin:0;padding:0;background-color:#f3f4f6;font-family:{{Font}};color:#111827;">
                <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;font-size:1px;line-height:1px;color:#f3f4f6;">{{preheader}}&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;</div>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="#f3f4f6" style="background-color:#f3f4f6;">
                  <tr>
                    <td class="outer" align="center" style="padding:40px 16px;font-family:{{Font}};">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;">
                        <tr>
                          <td style="padding:0 4px 24px;">
                            <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td width="36" height="36" align="center" valign="middle" bgcolor="{{Brand}}" style="width:36px;height:36px;background-color:{{Brand}};border-radius:9px;font-family:{{Font}};font-size:19px;line-height:36px;font-weight:700;color:#ffffff;">C</td>
                                <td style="padding-left:10px;font-family:{{Font}};font-size:20px;line-height:36px;font-weight:700;letter-spacing:-0.3px;color:#111827;">Cortex</td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td bgcolor="#ffffff" style="background-color:#ffffff;border:1px solid #e5e7eb;border-radius:12px;">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                              <tr>
                                <td height="4" bgcolor="{{Brand}}" style="height:4px;line-height:4px;font-size:4px;background-color:{{Brand}};border-radius:12px 12px 0 0;">&nbsp;</td>
                              </tr>
                              <tr>
                                <td class="card-pad" style="padding:36px 40px 32px;font-family:{{Font}};">
                                  <h1 class="heading" style="margin:0 0 20px;font-size:24px;line-height:32px;font-weight:700;letter-spacing:-0.3px;color:#111827;">{{heading}}</h1>
                                  {{body}}
                                  {{detailsBlock}}
                                  <table role="presentation" class="button" cellspacing="0" cellpadding="0" border="0" style="margin:8px 0 20px;">
                                    <tr>
                                      <td align="center" bgcolor="{{Brand}}" style="border-radius:8px;background-color:{{Brand}};">
                                        <!--[if mso]>
                                        <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{{safeLink}}" style="height:48px;v-text-anchor:middle;width:240px;" arcsize="17%" stroke="f" fillcolor="{{Brand}}">
                                          <w:anchorlock/>
                                          <center style="color:#ffffff;font-family:Arial,sans-serif;font-size:16px;font-weight:bold;">{{buttonText}}</center>
                                        </v:roundrect>
                                        <![endif]-->
                                        <!--[if !mso]><!-- -->
                                        <a href="{{safeLink}}" target="_blank" style="display:inline-block;padding:14px 28px;border-radius:8px;background-color:{{Brand}};font-family:{{Font}};font-size:16px;line-height:20px;font-weight:600;color:#ffffff;text-decoration:none;">{{buttonText}}</a>
                                        <!--<![endif]-->
                                      </td>
                                    </tr>
                                  </table>
                                  <p style="margin:0 0 28px;font-size:14px;line-height:22px;color:#6b7280;">{{expiry}}</p>
                                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                                    <tr>
                                      <td style="border-top:1px solid #e5e7eb;padding-top:24px;font-family:{{Font}};">
                                        <p style="margin:0 0 6px;font-size:13px;line-height:20px;color:#6b7280;">Button not working? Copy and paste this link into your browser:</p>
                                        <p style="margin:0 0 20px;font-size:13px;line-height:20px;word-break:break-all;"><a href="{{safeLink}}" target="_blank" style="color:{{Brand}};text-decoration:underline;">{{safeLink}}</a></p>
                                        <p style="margin:0;font-size:13px;line-height:20px;color:#6b7280;">{{securityNote}}</p>
                                      </td>
                                    </tr>
                                  </table>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td align="center" style="padding:28px 24px 0;font-family:{{Font}};">
                            <p style="margin:0 0 6px;font-size:13px;line-height:20px;font-weight:600;color:#4b5563;">Cortex</p>
                            <p style="margin:0 0 14px;font-size:12px;line-height:18px;color:#9ca3af;">Conversational commerce for your business</p>
                            <p style="margin:0 0 6px;font-size:12px;line-height:18px;color:#9ca3af;">{{reason}}</p>
                            <p style="margin:0;font-size:12px;line-height:18px;color:#9ca3af;">This is an automated message, so replies are not monitored.</p>
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
}
