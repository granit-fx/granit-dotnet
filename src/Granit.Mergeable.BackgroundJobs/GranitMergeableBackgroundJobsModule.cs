using Granit.BackgroundJobs;
using Granit.Mergeable.BackgroundJobs.Services;
using Granit.Mergeable.EntityFrameworkCore;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Mergeable.BackgroundJobs;

/// <summary>
/// Granit module that registers the recurring sweepers for <c>Granit.Mergeable</c>:
/// <list type="bullet">
///   <item><c>MergeIdempotencyCleanupJob</c> — daily delete of <c>merge_idempotency</c>
///   rows older than <c>MergeableOptions.IdempotencyRetention</c> so cached PII never lingers
///   past the configured retention window.</item>
/// </list>
/// Apps include this module in addition to <c>GranitMergeableEntityFrameworkCoreModule</c>;
/// <c>Granit.BackgroundJobs.Wolverine</c> discovers the <c>[RecurringJob]</c>-attributed jobs
/// and schedules them.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitMergeableEntityFrameworkCoreModule))]
public sealed class GranitMergeableBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IMergeIdempotencySweeper, MergeIdempotencyCleanupService>();
}
