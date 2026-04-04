using Granit.BackgroundJobs;
using Granit.Invoicing;
using Granit.Invoicing.BackgroundJobs.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Invoicing.BackgroundJobs;

/// <summary>Background jobs for invoicing: overdue detection and sync polling.</summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitInvoicingModule))]
public sealed class GranitInvoicingBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<IOverdueInvoiceDetectionService, DefaultOverdueInvoiceDetectionService>();
    }
}
