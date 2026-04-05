namespace Granit.Subscriptions;

/// <summary>
/// Creates invoices for Flat/PerSeat plans when a billing cycle completes.
/// </summary>
public interface IBillingCycleInvoiceOrchestrator
{
    /// <summary>
    /// Creates an invoice for a completed billing cycle.
    /// </summary>
    Task CreateInvoiceAsync(
        Guid subscriptionId,
        Guid tenantId,
        Guid planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}
