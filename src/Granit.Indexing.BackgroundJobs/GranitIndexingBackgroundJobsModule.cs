using Granit.BackgroundJobs;
using Granit.Indexing.BackgroundJobs.Extensions;
using Granit.Modularity;

namespace Granit.Indexing.BackgroundJobs;

/// <summary>
/// Granit module for the indexing background-jobs add-on.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitBackgroundJobsModule"/> (for the dispatcher + recurring
/// scanner) and <see cref="GranitIndexingModule"/> (for the <see cref="IIndexer{TKey}"/>
/// + <see cref="IIndexedEntrySource{TKey}"/> contracts). Hosts wire per-<c>TKey</c>
/// sources via <c>AddGranitIndexingRebuildSource&lt;TKey, TSource&gt;()</c>.
/// </remarks>
[DependsOn(typeof(GranitBackgroundJobsModule), typeof(GranitIndexingModule))]
public sealed class GranitIndexingBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexingBackgroundJobs();
}
