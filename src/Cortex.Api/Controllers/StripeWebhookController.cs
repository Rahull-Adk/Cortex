using System.Text;
using Cortex.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.Api.Controllers;

[AllowAnonymous]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ApiControllerBase
{
    private readonly SubscriptionService _subscriptions;

    public StripeWebhookController(SubscriptionService subscriptions) => _subscriptions = subscriptions;

    // The signature covers the exact bytes Stripe sent, so the body is read raw instead of model-bound.
    [HttpPost]
    [RequestSizeLimit(512 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signature))
            return Problem(SubscriptionErrors.InvalidWebhook);

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);

        var result = await _subscriptions.HandleWebhookAsync(payload, signature, ct);
        return FromResult(result, Ok);
    }
}
