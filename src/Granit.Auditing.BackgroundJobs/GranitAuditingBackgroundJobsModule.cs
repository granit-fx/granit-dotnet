using Granit.Auditing.BackgroundJobs.Internal;
using Granit.Auditing.EntityFrameworkCore;
using Granit.BackgroundJobs;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Auditing.BackgroundJobs;

/// <summary>
/// Granit module that registers the distributed audit-log retention cleanup
/// recurring job. Replaces the per-pod <c>AuditingCleanupWorker</c> so a
/// multi-replica deployment purges expired entries exactly once per schedule.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitAuditingEntityFrameworkCoreModule))]
public sealed class GranitAuditingBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<IAuditRetentionCleanupService, AuditRetentionCleanupService>();
}
