using Granit.Parties.Deduplication.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>
/// Wire-friendly view of a row in <c>parties_duplicate_candidates</c>. Returned by
/// <c>GET /parties/duplicates</c> and <c>GET /parties/{id}/duplicate-candidates</c>.
/// </summary>
/// <param name="Id">Stable id of this candidate-pair row (used to dismiss / merge).</param>
/// <param name="PartyId">Lower id of the ordered pair.</param>
/// <param name="CandidateId">Higher id of the ordered pair.</param>
/// <param name="Score">Aggregated confidence in <c>[0.0, 1.0]</c>.</param>
/// <param name="Tier">Detection tier — <c>"Deterministic"</c>, <c>"Blocking"</c>, <c>"Fuzzy"</c>.</param>
/// <param name="Signals">Per-signal contributions, sorted by score descending.</param>
/// <param name="DismissedAt">When an admin marked the pair as "not a duplicate". <c>null</c> when pending.</param>
/// <param name="CreatedAt">When the pair was first detected.</param>
/// <param name="UpdatedAt">When the pair was last refreshed by a re-scan. <c>null</c> on the initial detection.</param>
public sealed record PartyDuplicateCandidateResponse(
    Guid Id,
    Guid PartyId,
    Guid CandidateId,
    decimal Score,
    string Tier,
    IReadOnlyList<DuplicateMatchSignalResponse> Signals,
    DateTimeOffset? DismissedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>One per-signal contribution surfaced under <see cref="PartyDuplicateCandidateResponse.Signals"/>.</summary>
public sealed record DuplicateMatchSignalResponse(string Kind, decimal Score);

/// <summary>Paginated <c>GET /parties/duplicates</c> response.</summary>
public sealed record PartyDuplicateCandidatesPage(
    IReadOnlyList<PartyDuplicateCandidateResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Body of <c>POST /parties/duplicates/{id}/merge</c> — shortcut that resolves the
/// candidate row to its (survivor, loser) pair and forwards to the merge orchestrator.
/// </summary>
/// <param name="SurvivorId">Which end of the candidate pair survives. The loser is
/// inferred from the row (the other end). 422 if the id is not part of the pair.</param>
/// <param name="Choices">Per-field admin overrides — same semantics as
/// <see cref="PartyMergeRequest.Choices"/>.</param>
/// <param name="Reason">Optional admin justification — captured in the audit log.</param>
public sealed record PartyDuplicateMergeRequest(
    Guid SurvivorId,
    IReadOnlyDictionary<string, string>? Choices = null,
    string? Reason = null);
