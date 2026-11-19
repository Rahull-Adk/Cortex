using Cortex.Application.Subscriptions;
using Cortex.Application.Subscriptions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Authorize]
[Route("api/organizations/{organizationId:guid}/subscription")]
[Tags("Subscriptions")]
[ProducesProblem(StatusCodes.Status401Unauthorized, Description = "Missing or expired access token. Refresh the session and retry.")]
[ProducesProblem(StatusCodes.Status403Forbidden, Description = "The caller's role cannot do this. Viewing needs Owner or Admin; checkout and portal need Owner.")]
[ProducesProblem(StatusCodes.Status404NotFound, Description = "The business does not exist or the caller is not a member. See the endpoint for other cases.")]
public class SubscriptionsController : ApiControllerBase
{
    private readonly SubscriptionService _subscriptions;

    public SubscriptionsController(SubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet]
    [EndpointSummary("Get subscription and usage")]
    [EndpointDescription("Owner and Admin. Current plan, status, billing period and usage (AI answers, documents, storage). A business that never subscribed gets the free plan with status null. Use hasPaidAccess, not status, to decide what to unlock: a cancelled or past-due subscription keeps access until paidThroughUtc. After checkout, poll this until hasPaidAccess is true, because the Stripe webhook can arrive a moment after the redirect.")]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status200OK, "application/json", Description = "The subscription and current usage.")]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _subscriptions.GetAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPost("checkout")]
    [EndpointSummary("Start Stripe Checkout")]
    [EndpointDescription("Owner only. Returns a Stripe Checkout URL: redirect the browser to it. Stripe sends the user back to the billing tab with checkout=success or checkout=cancelled. There is no confirm call; the webhook activates the subscription. A 404 can also mean the plan code is unknown.")]
    [ProducesResponseType<BillingRedirectDto>(StatusCodes.Status200OK, "application/json", Description = "Redirect the browser to url.")]
    [ProducesValidationProblem(Description = "Invalid body, or the plan cannot be bought (the free plan).")]
    [ProducesProblem(StatusCodes.Status409Conflict, Description = "The business already has a subscription. Open the portal to change plan instead.")]
    public async Task<IActionResult> StartCheckout(
        Guid organizationId,
        [FromBody] StartCheckoutRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _subscriptions.StartCheckoutAsync(userId, organizationId, request, ct);
        return FromResult(result, Ok);
    }

    [HttpPost("portal")]
    [EndpointSummary("Open the Stripe billing portal")]
    [EndpointDescription("Owner only. Returns a Stripe customer portal URL where the owner switches plan, cancels or updates the card. Stripe returns the user to the billing tab. A 404 can also mean the business has never checked out.")]
    [ProducesResponseType<BillingRedirectDto>(StatusCodes.Status200OK, "application/json", Description = "Redirect the browser to url.")]
    public async Task<IActionResult> OpenPortal(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _subscriptions.OpenPortalAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }
}
