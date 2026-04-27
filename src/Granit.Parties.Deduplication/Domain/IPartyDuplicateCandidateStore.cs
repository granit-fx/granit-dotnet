using Granit.Parties.EntityFrameworkCore.Deduplication;

namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// Read + dismiss surface over the <c>parties_duplicate_candidates</c> review table.
/// Consumed by the admin endpoints (<see href="https://github.com/granit-fx/granit-dotnet/issues/1301">#1301</see>);
/// the recurring scan job (<see href="https://github.com/granit-fx/granit-dotnet/issues/1300">#1300</see>)
/// writes via <see cref="IDuplicateCandidateSink"/> instead.
/// </summary>
public interface IPartyDuplicateCandidateStore
{
    /// <summary>
    /// Paginated, filtered listing for the admin "duplicates inbox" view. Always tenant-scoped
    /// (the implementation reads <c>ICurrentTenant</c> ambiently). Sorted by score descending,
    /// most-recent-evidence first.
    /// </summary>
    /// <param name="tier">When supplied, restricts the result to the given detection tier.</param>
    /// <param name="minScore">Minimum aggregated score in <c>[0.0, 1.0]</c>.
    /// <c>null</c> falls back to <c>0.0</c>.</param>
    /// <param name="includeDismissed">When <c>false</c> (default) the store skips dismissed
    /// pairs — that is the "pending review" view. <c>true</c> surfaces dismissed pairs too,
    /// e.g. for a "review history" tab.</param>
    /// <param name="page">1-based page index.</param>
    /// <param name="pageSize">Items per page; clamped to <c>[1, 200]</c> by the impl.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DuplicateCandidatePage> ListAsync(
        DuplicateMatchTier? tier,
        decimal? minScore,
        bool includeDismissed,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Looks up one row by id. Returns <c>null</c> when not found in the current tenant.</summary>
    Task<PartyDuplicateCandidate?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns every pending (non-dismissed) candidate involving <paramref name="partyId"/>
    /// — both ends of the ordered pair. Used by the per-Party detail page and the online
    /// detection at create (<see href="https://github.com/granit-fx/granit-dotnet/issues/1302">#1302</see>).
    /// </summary>
    Task<IReadOnlyList<PartyDuplicateCandidate>> ListForPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the row as dismissed (admin "not a duplicate" decision). Idempotent — a second
    /// dismiss is a no-op. Returns <c>true</c> when the dismissal landed (row found, not
    /// previously dismissed); <c>false</c> when the row does not exist or was already
    /// dismissed.
    /// </summary>
    Task<bool> DismissAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>One page of a paginated <see cref="IPartyDuplicateCandidateStore.ListAsync"/> result.</summary>
public sealed record DuplicateCandidatePage(
    IReadOnlyList<PartyDuplicateCandidate> Items,
    int TotalCount,
    int Page,
    int PageSize);
