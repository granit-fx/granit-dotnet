using Granit.AI;
using Granit.Indexing.Embeddings.Extensions;
using Granit.Modularity;

namespace Granit.Indexing.Embeddings;

/// <summary>
/// Granit module for the opt-in embeddings + hybrid-RRF search add-on.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitIndexingModule"/> for the indexer / search-backend
/// contracts AND on <see cref="GranitAIModule"/> for the workspace-aware
/// <c>IAIEmbeddingGeneratorFactory</c> resolution path. Storage backends (EF
/// pgvector, Elasticsearch dense_vector) are wired by the host via their per-key
/// extensions, NOT by module discovery — embeddings stay opt-in.
/// </remarks>
[DependsOn(typeof(GranitIndexingModule), typeof(GranitAIModule))]
public sealed class GranitIndexingEmbeddingsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexingEmbeddings();
}
