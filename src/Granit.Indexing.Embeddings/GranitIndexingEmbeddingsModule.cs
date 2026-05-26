using Granit.Indexing.Embeddings.Extensions;
using Granit.Modularity;

namespace Granit.Indexing.Embeddings;

/// <summary>
/// Granit module for the opt-in embeddings + hybrid-RRF search add-on.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitIndexingModule"/> only — the storage backends (EF
/// pgvector, Elasticsearch dense_vector) are wired by the host via their per-key
/// extensions, NOT by module discovery. This keeps embeddings purely opt-in.
/// </remarks>
[DependsOn(typeof(GranitIndexingModule))]
public sealed class GranitIndexingEmbeddingsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexingEmbeddings();
}
