using Cortex.Application.Subscriptions;
using Cortex.Application.Subscriptions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[Authorize]
[Route("api/organizations/{organizationId:guid}/subscription")]
[ProducesProblem(StatusCodes.Status401Unauthorized)]
[ProducesProblem(StatusCodes.Status403Forbidden)]
[ProducesProblem(StatusCodes.Status404NotFound)]
public class SubscriptionsController : ApiControllerBase
{
    private readonly SubscriptionService _subscriptions;

    public SubscriptionsController(SubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _subscriptions.GetAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }

    [HttpPost("checkout")]
    [ProducesResponseType<BillingRedirectDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesValidationProblem]
    [ProducesProblem(StatusCodes.Status409Conflict)]
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
    [ProducesResponseType<BillingRedirectDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> OpenPortal(Guid organizationId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _subscriptions.OpenPortalAsync(userId, organizationId, ct);
        return FromResult(result, Ok);
    }
}
