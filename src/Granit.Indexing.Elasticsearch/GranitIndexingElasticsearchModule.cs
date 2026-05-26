using Granit.Modularity;

namespace Granit.Indexing.Elasticsearch;

/// <summary>
/// Granit module for the Elasticsearch-backed indexing storage.
/// </summary>
/// <remarks>
/// Module-level wiring is a no-op: <c>AddGranitIndexingElasticsearch</c> needs the cluster
/// URI / API key at registration time, so the module cannot self-register without input.
/// Consumers call the extension from their composition root after registering
/// <see cref="GranitIndexingModule"/>.
/// </remarks>
[DependsOn(typeof(GranitIndexingModule))]
public sealed class GranitIndexingElasticsearchModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // No-op: see remarks.
    }
}
