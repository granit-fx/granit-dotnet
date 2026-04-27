using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Integration event published on a successful merge. Cross-process consumers use this
/// to invalidate caches, refresh search indices, recompute materialised views, etc.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ResolvedChoices"/> uses <c>string</c> values (<c>"Survivor"</c> or
/// <c>"Loser"</c>) — not the <c>WinnerSide</c> enum from <c>Granit.Mergeable</c> — so
/// downstream services can deserialise this event without taking a dependency on the
/// merge framework. <c>Granit.Parties.Abstractions</c> stays a lightweight contracts
/// package.
/// </para>
/// <para>
/// Enqueued in the same transaction as the merge SaveChanges via the Wolverine outbox,
/// so delivery is at-least-once.
/// </para>
/// </remarks>
/// <param name="SurvivorId">The party that absorbed the loser.</param>
/// <param name="LoserId">The tombstoned party.</param>
/// <param name="TenantId">Owning tenant — <c>null</c> for host-scoped parties.</param>
/// <param name="MergedAt">When the merge was committed.</param>
/// <param name="ResolvedChoices">For each conflicted field, the chosen side as a string
/// (<c>"Survivor"</c> or <c>"Loser"</c>).</param>
/// <param name="RewriteCounts">For each registered reference rewriter, the number of
/// cross-module rows that were rewritten. Keyed by rewriter description.</param>
public sealed record PartyMergedEto(
    PartyId SurvivorId,
    PartyId LoserId,
    Guid? TenantId,
    DateTimeOffset MergedAt,
    IReadOnlyDictionary<string, string> ResolvedChoices,
    IReadOnlyDictionary<string, int> RewriteCounts) : IIntegrationEvent;
