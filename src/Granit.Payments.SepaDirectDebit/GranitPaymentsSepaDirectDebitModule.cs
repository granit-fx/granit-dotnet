using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Modularity;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.Payments.SepaDirectDebit.Exports;
using Granit.Payments.SepaDirectDebit.Metrics;
using Granit.Payments.SepaDirectDebit.Queries;
using Granit.QueryEngine.Extensions;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>
/// SEPA Direct Debit abstractions: mandate lifecycle, collection tracking,
/// and provider interfaces. Add .Internal, .GoCardless, or .Twikey for a concrete provider.
/// </summary>
[DependsOn(
    typeof(GranitPaymentsModule),
    typeof(GranitTimingModule))]
public sealed class GranitPaymentsSepaDirectDebitModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Query + Export definitions (ADR-020: owned by the base module).
        context.Services.AddQueryDefinition<Mandate, MandateQueryDefinition>();
        context.Services.AddExportDefinition<Mandate, MandateExportDefinition>();

        context.Services.AddMetricDefinition<Mandate, int, ActiveMandateCountMetricDefinition>();
        context.Services.AddMetricDefinition<Mandate, int, PendingMandateCountMetricDefinition>();
        context.Services.AddMetricDefinition<Mandate, int, CancelledMandateCountMetricDefinition>();
    }
}
