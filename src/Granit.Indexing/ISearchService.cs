using System.Diagnostics.CodeAnalysis;

namespace Granit.Indexing;

/// <summary>
/// Read-side façade orchestrating an <see cref="ISearchBackend{TKey, TResult}"/>, an
/// <see cref="ISearchResultAuthorizer{TKey}"/>, and the empty-result rate limiter.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorization boundary.</b> Tenant isolation is enforced by the backend. Per-
/// resource ACL is enforced by the authorizer. The orchestrator's only job is the
/// exponential over-fetch loop that resolves the page worth of authorised hits without
/// leaking page-by-page existence signals.
/// </para>
/// <para>
/// <b>Over-fetch loop.</b> Iteration 1 fetches
/// <c>pageSize × authorizer.RecommendedInitialMultiplier</c> rows from offset 0; the
/// window doubles every subsequent iteration. The loop stops when (a) the authorised
/// subset is large enough to fill the requested page, (b) the backend reports no more
/// rows, or (c) total fetched exceeds
/// <see cref="Options.GranitIndexingOptions.MaxAuthorizationDepth"/> — in which case the
/// response carries <see cref="SearchPage{TResult}.HitAuthorizationLimit"/> set to
/// <c>true</c>.
/// </para>
/// <para>
/// <b>Empty-result throttle.</b> When a search returns zero authorised hits and the
/// caller has exceeded
/// <see cref="Options.GranitIndexingOptions.MaxEmptyResultQueriesPerPrincipalPerMinute"/>
/// within the last minute, the orchestrator throws
/// <see cref="Exceptions.EmptyResultRateLimitedException"/>. Consumers convert it into
/// <c>Problem(429)</c> at the HTTP boundary.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed",
    Justification = "TKey is required for the open-generic DI registration " +
        "(typeof(ISearchService<,>) -> DefaultSearchService<,>): the implementation injects " +
        "ISearchBackend<TKey, TResult> and ISearchResultAuthorizer<TKey>. No interface member " +
        "references TKey, but dropping it would leave an arity-1 interface against an arity-2 " +
        "implementation, invalidating the open-generic registration and per-TKey backend resolution.")]
public interface ISearchService<TKey, TResult>
{
    /// <summary>Executes a search and returns the authorised page.</summary>
    /// <param name="request">User-supplied query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Exceptions.EmptyResultRateLimitedException">
    /// The principal exceeded the configured empty-result cap.
    /// </exception>
    Task<SearchPage<TResult>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default);
}
