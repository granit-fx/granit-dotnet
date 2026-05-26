using Granit.AI;
using Granit.Indexing.Embeddings.Diagnostics;
using Granit.Indexing.Embeddings.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.Embeddings.Internal;

/// <summary>
/// Decorator over <see cref="IIndexer{TKey}"/>. On each <c>IndexAsync(entry)</c>:
/// resolves an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> for the configured
/// <c>Granit.AI</c> workspace, generates the embedding, writes it onto
/// <see cref="IndexedEntry{TKey}.Embedding"/>, and delegates to the inner indexer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful skip.</b> Generator failures (workspace mis-config, transport,
/// timeout, dimension mismatch) are logged as warnings + tagged on the metric; the
/// entry is still written to the storage backend WITHOUT an embedding. Lexical search
/// keeps working, semantic search just won't pick it up until the next re-index.
/// </para>
/// <para>
/// <b>Dimension validation.</b> The decorator does NOT check that the generated vector
/// matches <see cref="GranitIndexingEmbeddingsOptions.Dimensions"/> — that's the storage
/// backend's job (pgvector REJECTS mismatched dimensions at INSERT time, ES rejects at
/// indexing time). Catching it here would duplicate the contract.
/// </para>
/// <para>
/// <b>Per-call client lifecycle.</b> The factory currently builds a fresh
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> per call (matches the
/// <c>IAIChatClientFactory</c> convention in <c>Granit.LanguageDetection.AI</c>); we
/// dispose it in a <c>using</c>. Pooling is a separate optimisation if benchmarks
/// justify it.
/// </para>
/// </remarks>
internal sealed partial class EmbeddingIndexer<TKey> : IIndexer<TKey>
{
    private readonly IIndexer<TKey> _inner;
    private readonly IAIEmbeddingGeneratorFactory _factory;
    private readonly GranitIndexingEmbeddingsOptions _options;
    private readonly EmbeddingsMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<EmbeddingIndexer<TKey>> _logger;

    public EmbeddingIndexer(
        IIndexer<TKey> inner,
        IAIEmbeddingGeneratorFactory factory,
        IOptions<GranitIndexingEmbeddingsOptions> options,
        EmbeddingsMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<EmbeddingIndexer<TKey>> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(logger);
        _inner = inner;
        _factory = factory;
        _options = options.Value;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task IndexAsync(IndexedEntry<TKey> entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string? tenantId = (entry.TenantId ?? _currentTenant.Id)?.ToString();
        IndexedEntry<TKey> enriched = entry;

        if (!string.IsNullOrWhiteSpace(entry.Content))
        {
            try
            {
                string? workspace = string.IsNullOrEmpty(_options.WorkspaceName) ? null : _options.WorkspaceName;
                using IEmbeddingGenerator<string, Embedding<float>> generator = await _factory
                    .CreateAsync(workspace, cancellationToken)
                    .ConfigureAwait(false);

                GeneratedEmbeddings<Embedding<float>> result = await generator
                    .GenerateAsync([entry.Content], options: null, cancellationToken)
                    .ConfigureAwait(false);

                Embedding<float>? embedding = result.Count > 0 ? result[0] : null;
                if (embedding is not null)
                {
                    enriched = entry with { Embedding = embedding.Vector };
                    _metrics.RecordEmbeddingGenerated(tenantId, _options.EmbeddingModelId);
                }
                else
                {
                    _metrics.RecordEmbeddingFailed(tenantId, "empty_result");
                    LogEmptyResult(tenantId ?? "global");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordEmbeddingFailed(tenantId, "transport");
                LogTransportFailure(tenantId ?? "global", ex.Message);
            }
        }

        await _inner.IndexAsync(enriched, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(TKey key, Guid? tenantId, CancellationToken cancellationToken = default) =>
        _inner.RemoveAsync(key, tenantId, cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Embedding generator returned an empty result for tenant {TenantId} — entry persisted without an embedding")]
    private partial void LogEmptyResult(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Embedding generation failed for tenant {TenantId}: {ErrorMessage} — entry persisted without an embedding")]
    private partial void LogTransportFailure(string tenantId, string errorMessage);
}
