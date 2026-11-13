using Cortex.Infrastructure.Auth;

namespace Cortex.Infrastructure.Tests.Auth;

public class EmailTemplatesTests
{
    private const string Link = "https://app.cortex.test/invitations/accept?token=abc_123-XYZ";

    [Fact]
    public void Verification_ContainsNameLinkAndExpiry()
    {
        var message = EmailTemplates.Verification("Aye Chan", "https://app.cortex.test/verify-email?token=t0ken");

        Assert.Equal("Confirm your email address", message.Subject);
        Assert.Contains("Hi Aye Chan,", message.Html);
        Assert.Contains("href=\"https://app.cortex.test/verify-email?token=t0ken\"", message.Html);
        Assert.Contains("24 hours", message.Html);
        Assert.Contains("https://app.cortex.test/verify-email?token=t0ken", message.Text);
        Assert.Contains("24 hours", message.Text);
    }

    [Fact]
    public void OrganizationInvitation_ContainsBusinessInviterRoleAndLink()
    {
        var message = EmailTemplates.OrganizationInvitation("staff@example.com", "Shwe Store", "Aye Chan", "Admin", Link);

        Assert.Equal("Aye Chan invited you to join Shwe Store on Cortex", message.Subject);
        Assert.Contains("Shwe Store", message.Html);
        Assert.Contains("Aye Chan", message.Html);
        Assert.Contains("Admin", message.Html);
        Assert.Contains("staff@example.com", message.Html);
        Assert.Contains("7 days", message.Html);
        Assert.Contains("Accept invitation", message.Html);
        Assert.Contains(Link, message.Text);
        Assert.Contains("Your role:   Admin", message.Text);
    }

    [Fact]
    public void OrganizationInvitation_EscapesHtmlInNamesTheUserControls()
    {
        var message = EmailTemplates.OrganizationInvitation(
            "staff@example.com", "<script>alert(1)</script>", "<img src=x onerror=alert(2)>", "Member", Link);

        Assert.DoesNotContain("<script>alert(1)</script>", message.Html);
        Assert.DoesNotContain("<img src=x", message.Html);
        Assert.Contains("&lt;script&gt;", message.Html);
    }

    [Fact]
    public void Verification_EscapesHtmlInName()
    {
        var message = EmailTemplates.Verification("<b>Bob</b>", "https://app.cortex.test/verify-email?token=t");

        Assert.DoesNotContain("<b>Bob</b>", message.Html);
        Assert.Contains("&lt;b&gt;Bob&lt;/b&gt;", message.Html);
    }

    [Fact]
    public void Link_IsEncodedInsideAttributes()
    {
        var message = EmailTemplates.Verification("Bob", "https://app.cortex.test/verify-email?token=a&next=\"><script>");

        Assert.DoesNotContain("\"><script>", message.Html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Layout_IsCompleteDocumentWithPlainTextAlternative(bool invitation)
    {
        var message = invitation
            ? EmailTemplates.OrganizationInvitation("staff@example.com", "Shwe Store", "Aye Chan", "Member", Link)
            : EmailTemplates.Verification("Aye Chan", Link);

        Assert.StartsWith("<!doctype html>", message.Html);
        Assert.EndsWith("</html>", message.Html.TrimEnd());
        Assert.DoesNotContain("{{", message.Html);
        Assert.DoesNotContain("<", message.Text);
        Assert.Contains("Cortex", message.Text);
    }
}
