using Granit.Events;
using Granit.Mergeable;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Domain event raised on the survivor when a merge is committed. Carries the
/// resolved per-field choices (every field that had a conflict, with the chosen
/// winner) and the rewrite counts reported by every <see cref="IReferenceRewriter{T}"/>
/// participant. In-process consumers (audit log writer, search index sync, materialised
/// views) subscribe to this event.
/// </summary>
/// <param name="SurvivorId">The party that absorbed the loser (still active).</param>
/// <param name="LoserId">The party that has been tombstoned.</param>
/// <param name="TenantId">Owning tenant — <c>null</c> for host-scoped parties.</param>
/// <param name="ResolvedChoices">For each field that conflicted, the side that was applied.
/// Empty when the survivor and loser had identical scalars.</param>
/// <param name="RewriteCounts">For each registered <see cref="IReferenceRewriter{T}"/>,
/// the number of cross-module rows that were rewritten. Keyed by
/// <see cref="IReferenceRewriter{T}.Description"/>.</param>
public sealed record PartyMergedEvent(
    PartyId SurvivorId,
    PartyId LoserId,
    Guid? TenantId,
    IReadOnlyDictionary<string, WinnerSide> ResolvedChoices,
    IReadOnlyDictionary<string, int> RewriteCounts) : IDomainEvent;
