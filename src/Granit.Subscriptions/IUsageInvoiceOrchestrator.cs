namespace Granit.Subscriptions;

/// <summary>
/// Groups the parameters needed to create a usage-based invoice.
/// </summary>
public sealed record CreateUsageInvoiceRequest(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    decimal AggregatedValue,
    string Unit,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

/// <summary>
/// Creates consolidated invoices (fixed + usage) for PerUnit/Tiered plans.
/// </summary>
public interface IUsageInvoiceOrchestrator
{
    /// <summary>
    /// Creates a consolidated invoice combining fixed subscription charges and usage-based charges.
    /// </summary>
    Task CreateInvoiceAsync(CreateUsageInvoiceRequest request, CancellationToken cancellationToken = default);
}
