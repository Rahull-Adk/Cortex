using Cortex.Application.Subscriptions;
using Cortex.Application.Subscriptions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[AllowAnonymous]
[Route("api/plans")]
[Tags("Plans")]
public class PlansController : ApiControllerBase
{
    private readonly SubscriptionService _subscriptions;

    public PlansController(SubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet]
    [EndpointSummary("List plans")]
    [EndpointDescription("Public plan catalog in display order, for the pricing page and plan picker. priceMinor is in the smallest currency unit (satang for THB, so 99000 is 990 THB).")]
    [ProducesResponseType<IReadOnlyList<PlanDto>>(StatusCodes.Status200OK, "application/json", Description = "Active plans.")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _subscriptions.ListPlansAsync(ct));
}
