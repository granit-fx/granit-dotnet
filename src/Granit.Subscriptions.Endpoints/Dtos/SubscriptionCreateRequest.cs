namespace Granit.Subscriptions.Endpoints.Dtos;

/// <summary>Request to create a new subscription.</summary>
public sealed record SubscriptionCreateRequest(
    Guid ContactId,
    Guid PlanId,
    string Currency,
    DateTimeOffset? TrialEndsAt = null);

/// <summary>Request to cancel a subscription.</summary>
public sealed record SubscriptionCancelRequest(
    string? Reason = null,
    bool AtPeriodEnd = false);

/// <summary>Request to change subscription plan.</summary>
public sealed record SubscriptionChangePlanRequest(
    Guid NewPlanId);

