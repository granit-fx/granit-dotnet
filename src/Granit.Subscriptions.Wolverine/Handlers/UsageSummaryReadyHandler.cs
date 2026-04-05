using Granit.Metering.Events;
using Granit.MultiTenancy;
using Granit.Subscriptions.Wolverine.Services;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Creates consolidated invoices (fixed + usage) for PerUnit/Tiered plans.
/// Delegates to <see cref="UsageInvoiceOrchestrator"/>.
/// </summary>
public class UsageSummaryReadyHandler
{
    public static async Task HandleAsync(
        UsageSummaryReadyEto eto,
        UsageInvoiceOrchestrator orchestrator,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            var request = new CreateUsageInvoiceRequest(
                eto.TenantId, eto.MeterDefinitionId, eto.MeterName,
                eto.AggregatedValue, eto.Unit, eto.PeriodStart, eto.PeriodEnd);

            await orchestrator.CreateInvoiceAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
