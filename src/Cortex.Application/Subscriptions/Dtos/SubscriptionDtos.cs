using Cortex.Domain.Enums;

namespace Cortex.Application.Subscriptions.Dtos;

public sealed record PlanDto(
    string Code,
    string Name,
    long PriceMinor,
    string Currency,
    int AiAnswersPerPeriod,
    int MaxDocuments,
    long MaxStorageBytes);

public sealed record UsageDto(int AiAnswersUsed, int DocumentCount, long StorageBytes);

// Status is null for a business that has never subscribed.
public sealed record SubscriptionDto(
    PlanDto Plan,
    SubscriptionStatus? Status,
    bool HasPaidAccess,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? PaidThroughUtc,
    bool CancelAtPeriodEnd,
    UsageDto Usage);

public sealed record StartCheckoutRequest(string PlanCode);

public sealed record BillingRedirectDto(string Url);
