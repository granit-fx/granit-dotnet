using System.Diagnostics;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Exceptions;
using Granit.Indexing.Options;
using Granit.MultiTenancy;

namespace Granit.Indexing.Internal;

/// <summary>
/// Default <see cref="ISearchService{TKey, TResult}"/> orchestrator: drives the
/// exponential over-fetch loop, applies the authorizer, enforces the empty-result
/// throttle, and records telemetry. Backend-agnostic — every backend ships its own
/// <see cref="ISearchBackend{TKey, TResult}"/>.
/// </summary>
internal sealed class DefaultSearchService<TKey, TResult> : ISearchService<TKey, TResult>
{
    private readonly ISearchBackend<TKey, TResult> _backend;
    private readonly ISearchResultAuthorizer<TKey> _authorizer;
    private readonly IEmptyResultRateLimiter _rateLimiter;
    private readonly GranitIndexingOptions _options;
    private readonly IndexingMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly TimeProvider _timeProvider;

    public DefaultSearchService(
        ISearchBackend<TKey, TResult> backend,
        ISearchResultAuthorizer<TKey> authorizer,
        IEmptyResultRateLimiter rateLimiter,
        GranitIndexingOptions options,
        IndexingMetrics metrics,
        ICurrentTenant currentTenant,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(authorizer);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _backend = backend;
        _authorizer = authorizer;
        _rateLimiter = rateLimiter;
        _options = options;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _timeProvider = timeProvider;
    }

    public async Task<SearchPage<TResult>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);

        int pageSize = Math.Min(request.PageSize, _options.MaxPageSize);
        int wantThrough = request.Page * pageSize;
        string? tenantTag = _currentTenant.Id?.ToString();
        string backendName = _backend.Name;
        long startTicks = _timeProvider.GetUtcNow().UtcTicks;

        using Activity? activity = IndexingActivitySource.Source.StartActivity(
            IndexingActivitySource.Search,
            ActivityKind.Internal);
        activity?.SetTag("backend", backendName);
        activity?.SetTag("page", request.Page);
        activity?.SetTag("page_size", pageSize);

        _metrics.RecordSearchQuery(tenantTag, backendName);

        // Exponential over-fetch loop. Cumulative authorised hits accumulate in
        // backend-score order; we slice the requested page at the end.
        (List<SearchHit<TKey, TResult>> authorisedHits, int backendHitCount, bool hitLimit) =
            await CollectAuthorisedHitsAsync(request, pageSize, wantThrough, tenantTag, backendName, cancellationToken)
                .ConfigureAwait(false);

        int skip = (request.Page - 1) * pageSize;
        TResult[] items = authorisedHits.Count <= skip
            ? []
            : authorisedHits.Skip(skip).Take(pageSize).Select(h => h.Result).ToArray();

        // Telemetry — emit before throttle so the metric does not lie even when the
        // call ultimately throws.
        _metrics.RecordBackendHits(tenantTag, backendName, backendHitCount);
        double latencySeconds = (_timeProvider.GetUtcNow().UtcTicks - startTicks) / (double)TimeSpan.TicksPerSecond;
        _metrics.RecordSearchLatency(tenantTag, backendName, latencySeconds);

        // Empty-result throttle: fire only when this call yielded zero authorised items
        // AND the caller identified itself. Anonymous / system callers bypass the cap.
        if (items.Length == 0 && !string.IsNullOrEmpty(request.PrincipalIdentifier))
        {
            string principalHash = PrincipalIdentifierHasher.Hash(request.PrincipalIdentifier!);
            if (_rateLimiter.RecordEmptyResultAndShouldThrottle(principalHash))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "empty_result_rate_limited");
                throw new EmptyResultRateLimitedException(principalHash);
            }
        }

        return new SearchPage<TResult>
        {
            Items = items,
            Page = request.Page,
            PageSize = pageSize,
            TotalAuthorized = authorisedHits.Count,
            HitAuthorizationLimit = hitLimit,
            BackendHitCount = backendHitCount,
        };
    }

    // Exponential over-fetch loop: repeatedly pulls a growing window from the backend,
    // authorises each page, and accumulates distinct authorised hits in backend-score
    // order until the requested page is covered, the backend is exhausted, or the
    // MaxAuthorizationDepth budget is hit.
    private async Task<(List<SearchHit<TKey, TResult>> Hits, int BackendHitCount, bool HitLimit)> CollectAuthorisedHitsAsync(
        SearchRequest request, int pageSize, int wantThrough, string? tenantTag, string backendName,
        CancellationToken cancellationToken)
    {
        int multiplier = Math.Max(_authorizer.RecommendedInitialMultiplier, 1);
        int window = pageSize * multiplier;
        int offset = 0;
        int backendHitCount = 0;
        bool hitLimit = false;
        List<SearchHit<TKey, TResult>> authorisedHits = [];
        HashSet<TKey> authorisedKeys = [];

        while (true)
        {
            BackendSearchPage<TKey, TResult> page = await _backend
                .SearchAsync(request, offset, window, cancellationToken)
                .ConfigureAwait(false);

            backendHitCount += page.Hits.Count;

            if (page.Hits.Count > 0)
            {
                IReadOnlyList<TKey> candidateKeys = page.Hits.Select(h => h.Key).ToArray();
                AuthorizedResult<TKey> authorised = await _authorizer
                    .FilterAsync(candidateKeys, cancellationToken)
                    .ConfigureAwait(false);

                HashSet<TKey> authorisedSet = [.. authorised.Authorized];
                long filteredOut = page.Hits.Count - authorisedSet.Count;
                _metrics.RecordAuthorizationFiltered(tenantTag, backendName, filteredOut);

                // authorisedKeys.Add doubles as the cross-page de-dup; the lazy Where keeps each
                // distinct authorised hit exactly once.
                authorisedHits.AddRange(page.Hits
                    .Where(hit => authorisedSet.Contains(hit.Key) && authorisedKeys.Add(hit.Key)));
            }

            if (authorisedHits.Count >= wantThrough)
            {
                break;
            }

            if (!page.HasMore || page.Hits.Count == 0)
            {
                break;
            }

            if (backendHitCount >= _options.MaxAuthorizationDepth)
            {
                hitLimit = true;
                break;
            }

            offset += page.Hits.Count;
            // Double the window for the next iteration; cap at the remaining authorization
            // depth so the very last iteration cannot blow past MaxAuthorizationDepth.
            int nextWindow = window * 2;
            int remaining = _options.MaxAuthorizationDepth - backendHitCount;
            window = Math.Max(1, Math.Min(nextWindow, remaining));
        }

        return (authorisedHits, backendHitCount, hitLimit);
    }
}
