using Granit.Indexing.Embeddings.Diagnostics;
using Granit.Indexing.Embeddings.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.Embeddings.Internal;

/// <summary>
/// <see cref="ISearchBackend{TKey, TResult}"/> wrapping a lexical inner backend +
/// an <see cref="IVectorSearchBackend{TKey, TResult}"/>. On every search:
/// <list type="number">
///   <item>Embeds <see cref="SearchRequest.Query"/> once via the configured
///         <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</item>
///   <item>Fetches a deep pool from both channels in parallel
///         (<c>poolSize = max(RrfFetchPoolSize, (offset + limit) * 2)</c>).</item>
///   <item>Fuses via <see cref="ReciprocalRankFusion.Fuse"/> with dense ranking.</item>
///   <item>Slices to <c>(offset, limit)</c> and returns the page.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Wrapping at the backend layer (not the service layer) preserves the existing
/// authorizer over-fetch loop in <c>DefaultSearchService</c> — the orchestrator
/// stays unchanged and benefits from RRF transparently.
/// </para>
/// <para>
/// <b>Graceful degradation.</b> When the embedding call fails (transport, timeout) the
/// hybrid backend falls back to the lexical inner alone with original offset/limit so
/// the user still gets results. The vector miss counter is bumped so operators can
/// alert on embedding-generator outages.
/// </para>
/// </remarks>
internal sealed partial class HybridSearchBackend<TKey, TResult> : ISearchBackend<TKey, TResult>
    where TKey : notnull
{
    private readonly ISearchBackend<TKey, TResult> _lexical;
    private readonly IVectorSearchBackend<TKey, TResult> _vector;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly GranitIndexingEmbeddingsOptions _options;
    private readonly EmbeddingsMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HybridSearchBackend<TKey, TResult>> _logger;

    public HybridSearchBackend(
        ISearchBackend<TKey, TResult> lexical,
        IVectorSearchBackend<TKey, TResult> vector,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IOptions<GranitIndexingEmbeddingsOptions> options,
        EmbeddingsMetrics metrics,
        ICurrentTenant currentTenant,
        TimeProvider timeProvider,
        ILogger<HybridSearchBackend<TKey, TResult>> logger)
    {
        ArgumentNullException.ThrowIfNull(lexical);
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _lexical = lexical;
        _vector = vector;
        _generator = generator;
        _options = options.Value;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public string Name => $"hybrid({_lexical.Name}+{_vector.Name})";

    public async Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? tenantId = _currentTenant.Id?.ToString();
        _metrics.RecordHybridQuery(tenantId);

        long startTicks = _timeProvider.GetTimestamp();
        int poolSize = Math.Max(_options.RrfFetchPoolSize, (offset + limit) * 2);

        ReadOnlyMemory<float>? embedding = await TryGenerateAsync(request.Query, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (embedding is null)
        {
            // Lexical fallback — preserves the original page semantics.
            BackendSearchPage<TKey, TResult> lexicalOnly = await _lexical
                .SearchAsync(request, offset, limit, cancellationToken)
                .ConfigureAwait(false);
            RecordLatency(tenantId, startTicks);
            return lexicalOnly;
        }

        Task<BackendSearchPage<TKey, TResult>> lexicalTask = _lexical.SearchAsync(
            request, offset: 0, limit: poolSize, cancellationToken);
        Task<BackendSearchPage<TKey, TResult>> semanticTask = _vector.SearchAsync(
            embedding.Value, request, offset: 0, limit: poolSize, cancellationToken);

        await Task.WhenAll(lexicalTask, semanticTask).ConfigureAwait(false);
        BackendSearchPage<TKey, TResult> lexicalPage = await lexicalTask.ConfigureAwait(false);
        BackendSearchPage<TKey, TResult> semanticPage = await semanticTask.ConfigureAwait(false);

        if (semanticPage.Hits.Count == 0)
        {
            _metrics.RecordVectorBackendMiss(tenantId);
        }

        IReadOnlyList<SearchHit<TKey, TResult>> fused = ReciprocalRankFusion.Fuse(
            lexicalPage.Hits, semanticPage.Hits, _options.RrfK);

        int totalFused = fused.Count;
        SearchHit<TKey, TResult>[] page = fused
            .Skip(offset)
            .Take(limit)
            .ToArray();

        bool hasMore = totalFused > offset + page.Length
            || lexicalPage.HasMore
            || semanticPage.HasMore;

        RecordLatency(tenantId, startTicks);
        return new BackendSearchPage<TKey, TResult>(page, hasMore);
    }

    private async Task<ReadOnlyMemory<float>?> TryGenerateAsync(
        string query, string? tenantId, CancellationToken cancellationToken)
    {
        try
        {
            GeneratedEmbeddings<Embedding<float>> result = await _generator
                .GenerateAsync([query ?? string.Empty], options: null, cancellationToken)
                .ConfigureAwait(false);

            if (result.Count == 0)
            {
                _metrics.RecordEmbeddingFailed(tenantId, "empty_result");
                LogQueryEmbedFailure(tenantId ?? "global", "empty_result");
                return null;
            }

            _metrics.RecordEmbeddingGenerated(tenantId, _options.EmbeddingModelId);
            return result[0].Vector;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordEmbeddingFailed(tenantId, "transport");
            LogQueryEmbedFailure(tenantId ?? "global", ex.Message);
            return null;
        }
    }

    private void RecordLatency(string? tenantId, long startTicks)
    {
        double durationSeconds = _timeProvider.GetElapsedTime(startTicks).TotalSeconds;
        _metrics.RecordHybridLatency(tenantId, durationSeconds);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Query embedding failed for tenant {TenantId} ({Reason}) — hybrid search degraded to lexical-only")]
    private partial void LogQueryEmbedFailure(string tenantId, string reason);
}
