using Granit.Scheduling;

namespace Granit.Subscriptions.Scheduling;

/// <summary>
/// Scheduled payload for retrying a failed payment (dunning).
/// Delivered to its Wolverine handler via <c>Granit.Scheduling</c>.
/// </summary>
/// <param name="InvoiceId">The invoice to retry payment for.</param>
/// <param name="TenantId">The tenant owning the subscription.</param>
/// <param name="Attempt">The retry attempt number (1-based).</param>
public sealed record RetryPaymentPayload(
    Guid InvoiceId,
    Guid TenantId,
    int Attempt) : IScheduledPayload;
