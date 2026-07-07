using Granit.BackgroundJobs.Extensions;
using Granit.Guids;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.BackgroundJobs;

/// <summary>
/// Granit module for recurring background jobs (provider-agnostic core).
/// </summary>
/// <remarks>
/// Default registrations use in-process channel dispatch and an in-memory store —
/// no database required; suitable for development and tests. For durable, cluster-safe
/// scheduling add <c>Granit.BackgroundJobs.Wolverine</c>; for a persistent store add
/// <c>Granit.BackgroundJobs.EntityFrameworkCore</c>.
/// <para>
/// All loaded module assemblies are scanned for <see cref="RecurringJobAttribute"/>,
/// so jobs declared in satellite packages (<c>Granit.{Module}.BackgroundJobs</c>)
/// are discovered and seeded automatically.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitBackgroundJobs(context.ModuleAssemblies);
}
