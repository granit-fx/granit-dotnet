using Granit.Events;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Events;

/// <summary>
/// Domain event raised when child parties (<c>ParentContactId == loser.Id</c>) are
/// re-parented onto the survivor as part of a merge. Emitted by the merge orchestrator
/// after the SQL bulk-update completes. The count is reported separately from
/// <see cref="PartyMergedEvent.RewriteCounts"/> because re-parenting is a Parties-internal
/// operation, not a cross-module reference rewrite.
/// </summary>
/// <param name="SurvivorId">The new parent.</param>
/// <param name="LoserId">The previous parent (now tombstoned).</param>
/// <param name="TenantId">Owning tenant — <c>null</c> for host-scoped parties.</param>
/// <param name="Count">Number of child parties re-parented.</param>
public sealed record PartyChildrenReparentedEvent(
    PartyId SurvivorId,
    PartyId LoserId,
    Guid? TenantId,
    int Count) : IDomainEvent;
