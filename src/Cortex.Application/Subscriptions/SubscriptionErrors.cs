using Cortex.Application.Common;

namespace Cortex.Application.Subscriptions;

public static class SubscriptionErrors
{
    public const string SubscriptionRequiredCode = "subscription_required";
    public const string QuotaExceededCode = "quota_exceeded";

    public static readonly Error SubscriptionRequired = Error.PaymentRequired(
        "An active subscription is required for this feature.", SubscriptionRequiredCode);
    public static readonly Error AiAnswerQuotaExceeded = Error.PaymentRequired(
        "The AI answer allowance for this billing period has been used up.", QuotaExceededCode);
    public static readonly Error DocumentQuotaExceeded = Error.PaymentRequired(
        "The knowledge document limit of the plan has been reached.", QuotaExceededCode);
    public static readonly Error StorageQuotaExceeded = Error.PaymentRequired(
        "The storage limit of the plan has been reached.", QuotaExceededCode);

    public static readonly Error PlanNotFound = Error.NotFound("Plan not found.");
    public static readonly Error PlanNotPurchasable = Error.Validation("This plan cannot be purchased.");
    public static readonly Error AlreadySubscribed = Error.Conflict(
        "This business already has a subscription. Manage it from the billing portal.");
    public static readonly Error NoBillingAccount = Error.NotFound("This business has no billing account yet.");
    public static readonly Error InvalidWebhook = Error.Validation("Invalid webhook signature.");
    public static readonly Error WebhookConflict = Error.Conflict("The subscription changed concurrently. Retry.");
}
