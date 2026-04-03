using Granit.Guids;
using Granit.Invoicing.Extensions;
using Granit.Modularity;
using Granit.Timing;
using Granit.Workflow;

namespace Granit.Invoicing;

/// <summary>
/// Granit module for agnostic invoicing (invoices, credit notes, tax, sync, PDF).
/// </summary>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitInvoicingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitInvoicing();
}
