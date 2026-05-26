namespace Granit.Indexing;

/// <summary>
/// Low-level read port: paged + ranked fetch from a single index backend, with no
/// authorization logic. Wrapped by <see cref="ISearchService{TKey, TResult}"/>, which
/// adds the exponential over-fetch loop and the
/// <see cref="ISearchResultAuthorizer{TKey}"/> filter.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant isolation is the backend's responsibility.</b> Implementations MUST scope
/// every query to <see cref="MultiTenancy.ICurrentTenant"/> (when available). The
/// orchestrator never injects a tenant predicate — that contract belongs to the
/// backend, both because tenant identifiers may live in physically-separated stores and
/// because relying on an orchestrator-supplied predicate is a single-point-of-failure
/// for ISO 27001 A.9.4 isolation.
/// </para>
/// <para>
/// <b>Per-resource ACL is NOT the backend's responsibility.</b> Backends return every
/// row matching the query within the active tenant; the over-fetch loop in
/// <c>DefaultSearchService</c> drops unauthorised rows via the consumer-supplied
/// <see cref="ISearchResultAuthorizer{TKey}"/>.
/// </para>
/// </remarks>
public interface ISearchBackend<TKey, TResult>
{
    /// <summary>Stable backend identifier emitted on metric tags (e.g. <c>"ef_tsvector"</c>, <c>"elasticsearch"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Executes a paged + ranked search against the backend. Implementations MUST honour
    /// <paramref name="offset"/> + <paramref name="limit"/> over the raw backend result
    /// set; the over-fetch loop computes those values from
    /// <see cref="SearchRequest.Page"/> × <see cref="SearchRequest.PageSize"/> × the
    /// authorizer multiplier.
    /// </summary>
    /// <param name="request">User-supplied query parameters.</param>
    /// <param name="offset">Zero-based row offset within the backend result set.</param>
    /// <param name="limit">Maximum rows to return for this iteration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Hits in score-descending order plus a flag indicating whether more rows exist
    /// past the end of this window.
    /// </returns>
    Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Backend-side response wrapping raw hits plus pagination metadata.</summary>
/// <param name="Hits">Hits in score-descending order.</param>
/// <param name="HasMore">
/// <c>true</c> when at least one additional row exists past the end of this window —
/// drives the over-fetch loop's "backend exhausted" branch without forcing the backend
/// to compute an exact total count (which is expensive on tsvector and ES alike).
/// </param>
public sealed record BackendSearchPage<TKey, TResult>(
    IReadOnlyList<SearchHit<TKey, TResult>> Hits,
    bool HasMore);
