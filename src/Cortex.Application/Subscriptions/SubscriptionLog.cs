using Cortex.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Cortex.Application.Subscriptions;

internal static partial class SubscriptionLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Checkout for plan {PlanCode} started for business {OrganizationId} by user {UserId}.")]
    public static partial void CheckoutStarted(this ILogger logger, Guid organizationId, Guid userId, string planCode);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Checkout for business {OrganizationId} rejected: {Reason}.")]
    public static partial void CheckoutRejected(this ILogger logger, Guid organizationId, string reason);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Billing portal opened for business {OrganizationId} by user {UserId}.")]
    public static partial void PortalOpened(this ILogger logger, Guid organizationId, Guid userId);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Warning, Message = "Billing webhook rejected: invalid signature.")]
    public static partial void WebhookRejected(this ILogger logger);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information, Message = "Billing webhook acknowledged without changes: {Reason}.")]
    public static partial void WebhookIgnored(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Information, Message = "Subscription of business {OrganizationId} is now {Status} on plan {PlanCode} (cancel at period end: {CancelAtPeriodEnd}, paid through: {PaidThroughUtc}).")]
    public static partial void SubscriptionUpdated(this ILogger logger, Guid organizationId, string planCode, SubscriptionStatus status, bool cancelAtPeriodEnd, DateTime? paidThroughUtc);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Warning, Message = "Billing webhook for business {OrganizationId} hit a concurrent update. The provider will retry.")]
    public static partial void WebhookConflict(this ILogger logger, Guid organizationId);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Business {OrganizationId} denied {Feature}: no paid subscription.")]
    public static partial void SubscriptionRequired(this ILogger logger, Guid organizationId, string feature);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Business {OrganizationId} denied {Feature}: {Quota} quota used up.")]
    public static partial void QuotaExceeded(this ILogger logger, Guid organizationId, string feature, string quota);
}
