using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Invoicing.BackgroundJobs;

/// <summary>Background jobs for invoicing: overdue detection and sync polling.</summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitInvoicingModule))]
public sealed class GranitInvoicingBackgroundJobsModule : GranitModule;
