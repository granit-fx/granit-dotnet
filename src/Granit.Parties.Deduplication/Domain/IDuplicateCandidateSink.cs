namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// Receives candidates from the duplicate-detection pipeline and persists them for admin
/// review. Default EF implementation lives in <c>Granit.Parties.Deduplication.BackgroundJobs</c>;
/// alternative sinks (no-op for ad-hoc scans, in-memory for tests) plug in via DI.
/// </summary>
public interface IDuplicateCandidateSink
{
    /// <summary>
    /// Upserts the candidates produced by scanning <paramref name="sourcePartyId"/> against
    /// the rest of <paramref name="tenantId"/>. The implementation is responsible for:
    /// <list type="bullet">
    ///   <item>ordering each pair (lower-id first) before insert/update;</item>
    ///   <item>skipping candidates whose pair was previously dismissed by an admin;</item>
    ///   <item>refreshing score / signals when the same pair re-appears in a later scan.</item>
    /// </list>
    /// Returns the count of pairs persisted (newly inserted or refreshed) — dismissed
    /// pairs are not counted.
    /// </summary>
    Task<int> UpsertAsync(
        IReadOnlyList<DuplicateCandidate> candidates,
        Guid sourcePartyId,
        Guid? tenantId,
        CancellationToken cancellationToken);
}
