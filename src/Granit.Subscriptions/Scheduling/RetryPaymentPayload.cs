using Granit.Scheduling;

namespace Granit.Subscriptions.Scheduling;

/// <summary>
/// Scheduled payload for retrying a failed payment (dunning).
/// Delivered to its Wolverine handler via <c>Granit.Scheduling</c>.
/// </summary>
/// <param name="InvoiceId">The invoice to retry payment for.</param>
/// <param name="TenantId">The tenant owning the subscription.</param>
/// <param name="Amount">The invoice amount to charge.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="MethodType">Payment method type (e.g., "card", "sepa_debit").</param>
/// <param name="ProviderName">The provider that handled the original payment (e.g., "stripe").</param>
/// <param name="Attempt">The retry attempt number (1-based).</param>
public sealed record RetryPaymentPayload(
    Guid InvoiceId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string MethodType,
    string ProviderName,
    int Attempt) : IScheduledPayload;
