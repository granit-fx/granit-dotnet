using Granit.Domain;

namespace Granit.Mergeable;

/// <summary>
/// Outcome of a merge operation (preview or live). Used by the admin UI to show what was
/// (or would be) rewritten and which fields conflicted.
/// </summary>
/// <typeparam name="TAggregate">The merged aggregate root type.</typeparam>
/// <param name="Merged">The survivor aggregate after the merge — <c>null</c> when <see cref="DryRun"/>.</param>
/// <param name="Conflicts">Per-field conflicts (non-empty even after a live merge — useful for the audit trail).</param>
/// <param name="RewriteCounts">For each registered rewriter, how many cross-module rows were
/// (or would be) rewritten. Keyed by <c>IReferenceRewriter.Description</c>.</param>
/// <param name="DryRun">Whether the operation was a dry-run preview (no DB changes committed).</param>
public sealed record MergeResult<TAggregate>(
    TAggregate? Merged,
    IReadOnlyList<FieldConflict> Conflicts,
    IReadOnlyDictionary<string, int> RewriteCounts,
    bool DryRun)
    where TAggregate : AggregateRoot;
