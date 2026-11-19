using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cortex.Api;

public sealed class ApiOverviewTransformer : IOpenApiDocumentTransformer
{
    private const string Overview = """
        Backend API for Cortex: businesses, members, invitations, plans and billing. Knowledge and AI endpoints come next.

        ## Authentication
        Sign in with `POST /api/auth/login` or `POST /api/auth/google-session`. The API returns the tokens and also sets the
        `cortex_access_token` and `cortex_refresh_token` HttpOnly cookies. Browser calls should use `credentials: "include"`;
        server calls can send `Authorization: Bearer <accessToken>`. Access tokens are short-lived: call `POST /api/auth/refresh`
        on a 401 and retry once.

        ## Businesses and roles
        Business endpoints take `{organizationId}` in the URL. Roles are `Owner`, `Admin` and `Member`, and the role is checked on
        every request (it is not in the JWT). A caller who is not a member gets **404**, the same as for a business that does not exist.
        A member whose role is not allowed gets **403**.

        ## Errors
        Every error is `application/problem+json`. Show `detail` to the user, map `errors` (camelCase keys) onto form fields on a 400,
        and include `traceId` in bug reports.

        ## Paid features (402)
        Locked features answer **402** with a `code`: `subscription_required` (show the upgrade prompt) or `quota_exceeded`
        (the plan's AI answer, document or storage limit is used up).
        """;

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Cortex API";
        document.Info.Description = Overview;
        return Task.CompletedTask;
    }
}
