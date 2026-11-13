using Cortex.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[AllowAnonymous]
[Route("api/plans")]
public class PlansController : ApiControllerBase
{
    private readonly SubscriptionService _subscriptions;

    public PlansController(SubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _subscriptions.ListPlansAsync(ct));
}
