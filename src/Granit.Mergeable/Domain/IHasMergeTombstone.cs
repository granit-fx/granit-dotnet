namespace Granit.Mergeable.Domain;

/// <summary>
/// Tombstone state for an aggregate that has been absorbed into another instance via a merge.
/// Independent of the merge behaviour (<see cref="IMergeable{TSelf}"/>) so EF query filters,
/// admin listings, audit views, and a future un-merge endpoint can target this contract
/// without knowing the merge orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// On the survivor: <see cref="MergedIntoId"/> and <see cref="MergedAt"/> are <c>null</c>.
/// On the loser (after merge): <see cref="MergedIntoId"/> points to the survivor, <see cref="MergedAt"/>
/// is the merge timestamp.
/// </para>
/// <para>
/// Chain merges (A → B → C) are collapsed at merge time: the orchestrator runs
/// <c>UPDATE SET MergedIntoId = newSurvivor WHERE MergedIntoId = oldSurvivor</c> so a single
/// hop is always enough to resolve a stale id to the current survivor — see
/// <c>PartyIdTombstoneExtensions.ResolveCurrentAsync</c>.
/// </para>
/// </remarks>
public interface IHasMergeTombstone
{
    /// <summary>Survivor id when this aggregate has been merged out; <c>null</c> otherwise.</summary>
    Guid? MergedIntoId { get; }

    /// <summary>Merge timestamp when this aggregate has been merged out; <c>null</c> otherwise.</summary>
    DateTimeOffset? MergedAt { get; }
}
