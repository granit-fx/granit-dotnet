namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// Three-tier duplicate-detection pipeline for <c>Granit.Parties</c>. Two entry points:
/// <list type="bullet">
///   <item><see cref="FindCandidatesAsync"/> — online, fed by the admin create-wizard
///         before the new party is persisted.</item>
///   <item><see cref="ScanTenantAsync"/> — batch, intended for the nightly background job
///         (<see href="https://github.com/granit-fx/granit-dotnet/issues/1300">#1300</see>)
///         that materialises the <c>parties_duplicate_candidates</c> review table.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Default implementation in <c>Granit.Parties.Deduplication</c> composes three internal
/// matchers — Tier-1 deterministic on canonical columns, Tier-2 pg_trgm trigram blocking
/// on Name (PostgreSQL) with a slower <c>LIKE</c>-based fallback on other providers, and
/// Tier-3 weighted-sum re-rank on the Tier-2 candidate set.
/// </para>
/// <para>
/// Always tenant-scoped — cross-tenant matches are not surfaced regardless of input.
/// </para>
/// </remarks>
public interface IPartyDuplicateDetector
{
    /// <summary>
    /// Returns candidate duplicates of <paramref name="draft"/> in the same tenant. Empty
    /// list when nothing crosses the configured thresholds. Ordered by <see cref="DuplicateCandidate.Score"/>
    /// descending so the UI can surface the most likely match first.
    /// </summary>
    Task<IReadOnlyList<DuplicateCandidate>> FindCandidatesAsync(
        PartyDraft draft,
        CancellationToken cancellationToken);

    /// <summary>
    /// Iterates every alive party in <paramref name="tenantId"/> and runs the full pipeline
    /// against the rest of the tenant. Returns the count of <em>candidate pairs</em> found
    /// (each pair counted once even though both ends could detect the other).
    /// </summary>
    /// <remarks>
    /// Intended to be driven by the recurring background scan job
    /// (<see href="https://github.com/granit-fx/granit-dotnet/issues/1300">#1300</see>) which
    /// also persists the candidates to a review table. The default detector returns the
    /// count and discards the in-memory candidates; persistence is the job's concern.
    /// </remarks>
    Task<int> ScanTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
