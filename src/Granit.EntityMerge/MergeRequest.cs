namespace Granit.EntityMerge;

/// <summary>
/// Input to <c>IMergeService.MergeAsync</c>. Identifies the survivor + loser, carries the
/// per-field admin overrides, and supports dry-run + Stripe-style idempotency.
/// </summary>
/// <param name="SurvivorId">Aggregate id that will absorb the loser. Must be alive (no tombstone).</param>
/// <param name="LoserId">Aggregate id that will be tombstoned. Must be alive and != survivor.</param>
/// <param name="Choices">Per-field admin overrides. Use <see cref="MergeFieldChoices.Empty"/> to defer to defaults.</param>
/// <param name="DryRun">When <c>true</c>, the orchestrator computes <c>FieldConflict</c>s and
/// <c>RewriteCounts</c> in count-only mode and rolls back the transaction. Use this to power
/// the "preview" UI before the admin confirms.</param>
/// <param name="Reason">Free-form admin justification — captured in audit log. Optional.</param>
/// <param name="IdempotencyKey">Optional key for retry-safe replay. When supplied, a second
/// call with the same key + same payload returns the cached result. Different payload + same
/// key returns 409. Cached for 24h.</param>
public sealed record MergeRequest(
    Guid SurvivorId,
    Guid LoserId,
    MergeFieldChoices Choices,
    bool DryRun = false,
    string? Reason = null,
    string? IdempotencyKey = null);
