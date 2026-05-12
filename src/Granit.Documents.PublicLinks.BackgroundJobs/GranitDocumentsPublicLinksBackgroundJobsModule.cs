using Granit.BackgroundJobs;
using Granit.Modularity;

namespace Granit.Documents.PublicLinks.BackgroundJobs;

/// <summary>
/// Granit module wiring the recurring public-link cleanup job. Recurring jobs are
/// auto-discovered by <c>Granit.BackgroundJobs</c> via the
/// <see cref="RecurringJobAttribute"/> on each <see cref="IBackgroundJob"/> record.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitDocumentsPublicLinksModule))]
public sealed class GranitDocumentsPublicLinksBackgroundJobsModule : GranitModule;
