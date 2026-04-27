using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Parties.Deduplication;
using Granit.Parties.Deduplication.BackgroundJobs.Diagnostics;
using Granit.Parties.Deduplication.BackgroundJobs.Internal;
using Granit.Parties.Deduplication.BackgroundJobs.Services;
using Granit.Parties.Deduplication.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Parties.Deduplication.BackgroundJobs;

/// <summary>
/// Granit module marker for the recurring Party duplicate-detection scan job. Wires the
/// EF-backed sink, the per-tenant scan service, and the metrics. Depends on
/// <see cref="GranitBackgroundJobsModule"/> for the Wolverine-driven recurring schedule
/// and on <see cref="GranitPartiesDeduplicationModule"/> for the detector.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitPartiesDeduplicationModule))]
public sealed class GranitPartiesDeduplicationBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<PartiesDeduplicationMetrics>();
        context.Services.TryAddScoped<IDuplicateCandidateSink, EfDuplicateCandidateSink>();
        context.Services.TryAddTransient<IPartyDuplicateScanService, PartyDuplicateScanService>();
    }
}
