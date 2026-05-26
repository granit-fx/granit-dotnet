namespace Granit.Indexing.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// Defaults are tuned for the common admin-tenant case: paging at 20 results,
/// 3× over-fetch multiplier (one backend round-trip suffices when the principal sees
/// ≥ 33 % of hits), 5 000-row authorization ceiling (covers up to ~250 fully-paginated
/// pages before bailing), 10 empty queries / minute / principal cap to slow down
/// existence-oracle probing.
/// </remarks>
public sealed class GranitIndexingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing";

    /// <summary>Default page size when a request omits one. Default: <c>20</c>.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Maximum page size accepted from a request. Default: <c>100</c>.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Fallback over-fetch multiplier applied when no <see cref="ISearchResultAuthorizer{TKey}"/>
    /// is registered or the authorizer omits its own recommendation. Default: <c>3</c>.
    /// </summary>
    /// <remarks>
    /// Per-call multiplier comes from
    /// <see cref="ISearchResultAuthorizer{TKey}.RecommendedInitialMultiplier"/>; this value is
    /// the floor used when no authorizer is in play.
    /// </remarks>
    public int AuthorizationOverfetchMultiplier { get; set; } = 3;

    /// <summary>
    /// Hard ceiling on total backend rows scanned while filtering for a single search
    /// response. Default: <c>5 000</c>. When breached, the orchestrator stops fetching and
    /// flags the response with <see cref="SearchPage{TResult}.HitAuthorizationLimit"/>.
    /// </summary>
    /// <remarks>
    /// The limit caps worst-case latency and database load when a restricted principal
    /// queries a low-selectivity term. The UX hint surfaces aggregated — never per-query —
    /// to deny attackers a per-call signal.
    /// </remarks>
    public int MaxAuthorizationDepth { get; set; } = 5_000;

    /// <summary>
    /// Maximum number of search queries per principal per minute that may legally return an
    /// empty page. Beyond this cap the orchestrator throws
    /// <see cref="Exceptions.EmptyResultRateLimitedException"/> and the consumer endpoint
    /// converts it to <c>Problem(429)</c>. Default: <c>10</c>.
    /// </summary>
    /// <remarks>
    /// Slows down a classical existence oracle: an attacker tries many slightly-different
    /// terms, looking for the one that hits a private record. Empty results are the
    /// signal, so the limit applies only to empty pages.
    /// </remarks>
    public int MaxEmptyResultQueriesPerPrincipalPerMinute { get; set; } = 10;
}
