using Granit.Domain;

namespace Granit.Mergeable.Domain;

/// <summary>
/// Aggregate marker: this aggregate root knows how to absorb a <em>loser</em> instance into
/// the current <em>survivor</em>, applying per-field admin choices to resolve scalar conflicts.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MergeFrom"/> only handles <b>scalar fields</b> (Name, Status flags, owned
/// value objects, dictionaries). Child collections referenced via shadow foreign keys
/// (<c>HasMany.WithOne().HasForeignKey("XId")</c>) MUST NOT be touched in-memory — they are
/// rewritten by an <see cref="IReferenceRewriter{TSelf}"/> registered alongside, which
/// issues a SQL bulk-update on the shadow FK in the same transaction. Doing
/// <c>survivor.Children.AddRange(loser.Children)</c> would conflict with EF's PK tracking
/// and shadow-FK tracking; the framework forbids it via the merge orchestrator.
/// </para>
/// <para>
/// The merge orchestrator (<c>IMergeService&lt;TSelf&gt;</c>) drives the workflow:
/// validation → lock → preview → apply → rewriters → tombstone → audit → outbox event.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The aggregate root type implementing this interface.</typeparam>
public interface IMergeable<TSelf> : IHasMergeTombstone
    where TSelf : AggregateRoot, IMergeable<TSelf>
{
    /// <summary>
    /// Returns the per-field conflicts between this (survivor) and <paramref name="loser"/>.
    /// Each <see cref="FieldConflict"/> exposes the surviving value, the loser value, and a
    /// recommended default (<see cref="WinnerSide"/>) used to pre-tick the admin UI.
    /// Returns an empty list if the two aggregates have no scalar differences.
    /// </summary>
    IReadOnlyList<FieldConflict> GetConflicts(TSelf loser);

    /// <summary>
    /// Folds <paramref name="loser"/> into this aggregate. Throws <see cref="MergeException"/>
    /// when a hard invariant is violated (e.g. tenant mismatch, archived aggregate, currency
    /// mismatch). Caller is responsible for setting the loser's tombstone after this call.
    /// </summary>
    /// <param name="loser">The aggregate being merged out (will be tombstoned by the orchestrator).</param>
    /// <param name="choices">Per-field admin overrides; missing keys fall back to the default
    /// recommended by <see cref="GetConflicts"/>.</param>
    void MergeFrom(TSelf loser, MergeFieldChoices choices);
}
