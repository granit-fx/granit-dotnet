namespace Granit.Indexing;

/// <summary>
/// Extensibility port: consumer-defined per-resource ACL filter applied to backend hits.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the authorization boundary.</b> <c>Granit.Indexing</c> enforces tenant
/// isolation only. Anything beyond — workspace ACL, role-based row filtering, public-
/// link grants — lives in the consumer module and is plugged in here. The
/// orchestrator calls <see cref="FilterAsync"/> for every over-fetch iteration and
/// returns only the authorised subset to the caller.
/// </para>
/// <para>
/// <b>Why not store ACL in the index?</b> Two reasons. (1) Permission changes are out-
/// of-band of the indexed corpus; persisting ACL forces cache invalidation on every
/// permission grant/revoke and creates eventually-consistent oracles. (2) The index
/// becomes a copy of the source-of-truth ACL store, multiplying the GDPR/ISO 27001
/// audit surface. The over-fetch loop trades a backend round-trip for an authoritative
/// per-query ACL check.
/// </para>
/// <para>
/// <b>NullSearchResultAuthorizer is the default.</b> When no implementation is registered,
/// <c>AddGranitIndexing</c> wires
/// <see cref="Internal.NullSearchResultAuthorizer{TKey}"/>, which authorises every hit
/// — appropriate for tenants where tenant isolation is the complete authorization
/// story (e.g. an internal admin search). Consumers with finer ACL MUST register their
/// own implementation explicitly.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Resource primary key.</typeparam>
public interface ISearchResultAuthorizer<TKey>
{
    /// <summary>
    /// Multiplier applied to <see cref="SearchRequest.PageSize"/> on the FIRST over-fetch
    /// iteration: a permissive principal (e.g. tenant admin who sees ~all resources)
    /// returns <c>1</c>; a restricted principal who sees ~10 % of hits should return
    /// <c>10</c>+. The orchestrator doubles the window each iteration past the first.
    /// </summary>
    /// <remarks>
    /// Use rule-of-thumb <c>ceil(1 / expected_authorized_ratio)</c>. Too low ⇒ extra
    /// round-trips on the common path. Too high ⇒ wasted backend rows on the rare
    /// path. The default for unknown principals is <c>3</c> — covers admin/restricted
    /// alike within two iterations.
    /// </remarks>
    int RecommendedInitialMultiplier { get; }

    /// <summary>
    /// Returns the authorised subset of <paramref name="candidates"/>. Order MAY be
    /// preserved (the orchestrator restores backend score ordering anyway via the
    /// original hit list).
    /// </summary>
    /// <param name="candidates">Backend-side keys to filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AuthorizedResult<TKey>> FilterAsync(
        IReadOnlyList<TKey> candidates,
        CancellationToken cancellationToken = default);
}

/// <summary>Authorised subset returned by <see cref="ISearchResultAuthorizer{TKey}.FilterAsync"/>.</summary>
/// <param name="Authorized">Keys the principal is allowed to see.</param>
public sealed record AuthorizedResult<TKey>(IReadOnlyList<TKey> Authorized);
