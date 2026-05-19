using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Extensions;
using Granit.BackgroundJobs.Options;
using Granit.Guids;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.BackgroundJobs;

/// <summary>
/// Granit module for recurring background jobs (provider-agnostic core).
/// </summary>
/// <remarks>
/// Default registrations use in-process channel dispatch and in-memory stores.
/// For durable, cluster-safe scheduling, add <c>Granit.BackgroundJobs.Wolverine</c>.
/// <para>
/// Mode selection is driven by <see cref="BackgroundJobsOptions.Mode"/>
/// (bound from the <c>"BackgroundJobs"</c> configuration section):
/// <list type="bullet">
///   <item><see cref="JobStoreMode.InMemory"/> — no DB required; suitable for development and tests.</item>
///   <item><see cref="JobStoreMode.Durable"/> — EF Core store; requires <see cref="BackgroundJobsOptions.ConnectionString"/>.</item>
/// </list>
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitBackgroundJobs();
}
