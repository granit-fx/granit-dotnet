using Granit.Mergeable;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>
/// Body of <c>POST /parties/{survivorId}/merge</c>. Identifies the loser, carries the
/// per-field admin overrides, and an optional reason captured in the audit log.
/// </summary>
/// <param name="LoserId">Id of the party to merge into the survivor (will be tombstoned).</param>
/// <param name="Choices">Per-field admin overrides. Keys are field paths
/// (<c>"Name"</c>, <c>"TaxStatus"</c>, <c>"Metadata.segment"</c>, …); values are
/// <c>"Survivor"</c> or <c>"Loser"</c>. Missing keys fall back to the recommended
/// default returned by the preview endpoint. Use an empty dictionary to defer to
/// defaults entirely.</param>
/// <param name="Reason">Free-form admin justification — captured in the audit log.</param>
/// <param name="DryRun">When <c>true</c>, the orchestrator computes the conflicts and
/// rewrite counts without committing — same shape as the preview endpoint, useful for
/// re-validating just before committing the live merge.</param>
public sealed record PartyMergeRequest(
    Guid LoserId,
    IReadOnlyDictionary<string, string>? Choices = null,
    string? Reason = null,
    bool DryRun = false);

/// <summary>
/// Response body for both <c>GET .../merge/preview</c> and <c>POST .../merge</c>.
/// Mirrors <see cref="MergeResult{TAggregate}"/> in a wire-friendly shape (no aggregate
/// payload — callers fetch the survivor via <c>GET /parties/{id}</c> after the merge).
/// </summary>
/// <param name="SurvivorId">The party that absorbed the loser. Always set.</param>
/// <param name="LoserId">The tombstoned party. Always set.</param>
/// <param name="Conflicts">Per-field scalar conflicts between survivor and loser, with
/// the recommended winner pre-populated.</param>
/// <param name="RewriteCounts">For each registered cross-module rewriter
/// (<c>"Invoice.PartyId"</c>, <c>"Subscription.PartyId"</c>, …), the number of rows
/// that were (or would be) rewritten. Powers the "what will change" preview.</param>
/// <param name="DryRun">Whether the operation was a dry-run (no DB changes).</param>
public sealed record PartyMergeResponse(
    Guid SurvivorId,
    Guid LoserId,
    IReadOnlyList<FieldConflictResponse> Conflicts,
    IReadOnlyDictionary<string, int> RewriteCounts,
    bool DryRun);

/// <summary>
/// Wire shape for a single field-level conflict. <see cref="FieldConflict.SurvivorValue"/>
/// and <see cref="FieldConflict.LoserValue"/> are <c>object?</c> in the domain — surfaced
/// as their <c>ToString()</c> representation here so the JSON payload stays predictable
/// regardless of the underlying type.
/// </summary>
/// <param name="FieldPath">Dot-separated path; e.g. <c>"Name"</c>, <c>"Metadata.segment"</c>.</param>
/// <param name="SurvivorValue">Survivor's current value, stringified (or <c>null</c>).</param>
/// <param name="LoserValue">Loser's current value, stringified (or <c>null</c>).</param>
/// <param name="Default">Recommended winner — <c>"Survivor"</c> or <c>"Loser"</c>.</param>
public sealed record FieldConflictResponse(
    string FieldPath,
    string? SurvivorValue,
    string? LoserValue,
    string Default);
