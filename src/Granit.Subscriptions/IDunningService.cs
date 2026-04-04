namespace Granit.Subscriptions;

/// <summary>
/// Handles dunning logic: transitions subscriptions to PastDue on payment failure,
/// schedules retry attempts with exponential backoff, and suspends after exhausting retries.
/// </summary>
public interface IDunningService
{
    /// <summary>
    /// Processes a payment failure for the given tenant. Marks the subscription as PastDue,
    /// schedules a retry, or suspends after max retries.
    /// </summary>
    /// <param name="tenantId">Tenant whose subscription payment failed.</param>
    /// <param name="invoiceId">Invoice that triggered the payment attempt.</param>
    /// <param name="amount">Failed payment amount.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="methodType">Payment method type (e.g., "card", "sepa_debit").</param>
    /// <param name="providerName">Payment provider name (e.g., "Stripe", "Mollie").</param>
    /// <param name="cancellationToken"></param>
    Task HandlePaymentFailureAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string methodType,
        string providerName,
        CancellationToken cancellationToken = default);
}
