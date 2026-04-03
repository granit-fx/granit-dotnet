using Granit.Scheduling;

namespace Granit.Subscriptions.Scheduling;

/// <summary>
/// Scheduled payload for applying a plan change at a future date.
/// Delivered to its Wolverine handler via <c>Granit.Scheduling</c>.
/// </summary>
/// <param name="SubscriptionId">The subscription to change.</param>
/// <param name="NewPlanId">The target plan.</param>
public sealed record ApplyPlanChangePayload(
    Guid SubscriptionId,
    Guid NewPlanId) : IScheduledPayload;
