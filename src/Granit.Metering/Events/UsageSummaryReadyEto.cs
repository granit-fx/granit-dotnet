using Granit.Events;

namespace Granit.Metering.Events;

/// <summary>
/// Published after the aggregation job completes a billing-period rollup.
/// Consumed by Subscriptions.Wolverine to include usage line items in invoices.
/// </summary>
public sealed record UsageSummaryReadyEto(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    string Unit,
    decimal AggregatedValue,
    long EventCount,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd) : IIntegrationEvent;
