using Granit.BackgroundJobs;
using Granit.EntityMerge.BackgroundJobs.Services;
using Granit.EntityMerge.EntityFrameworkCore;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.EntityMerge.BackgroundJobs;

/// <summary>
/// Granit module that registers the recurring sweepers for <c>Granit.EntityMerge</c>:
/// <list type="bullet">
///   <item><c>MergeIdempotencyCleanupJob</c> — daily delete of <c>merge_idempotency</c>
///   rows older than <c>EntityMergeOptions.IdempotencyRetention</c> so cached PII never lingers
///   past the configured retention window.</item>
/// </list>
/// Apps include this module in addition to <c>GranitEntityMergeEntityFrameworkCoreModule</c>;
/// <c>Granit.BackgroundJobs.Wolverine</c> discovers the <c>[RecurringJob]</c>-attributed jobs
/// and schedules them.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitEntityMergeEntityFrameworkCoreModule))]
public sealed class GranitEntityMergeBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IMergeIdempotencySweeper, MergeIdempotencyCleanupService>();
}
