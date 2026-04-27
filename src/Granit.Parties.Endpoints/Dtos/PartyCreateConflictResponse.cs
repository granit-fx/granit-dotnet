namespace Granit.Parties.Endpoints.Dtos;

/// <summary>
/// Body of the <c>409 Conflict</c> emitted by <c>POST /parties</c> when online duplicate
/// detection (story <see href="https://github.com/granit-fx/granit-dotnet/issues/1302">#1302</see>)
/// surfaces at least one Tier-1 deterministic match.
/// </summary>
/// <remarks>
/// <para>
/// The client has two recoveries:
/// </para>
/// <list type="bullet">
///   <item>Re-submit with <c>?force=true</c> to bypass the check and create the party
///         anyway (the admin acknowledges the duplicate is intentional).</item>
///   <item>Open the merge wizard against the suggested candidate via
///         <c>POST /parties/{survivorId}/merge</c> after creating the new row, OR drop
///         the create entirely and adopt the existing party.</item>
/// </list>
/// </remarks>
/// <param name="Reason">Always <c>"DuplicatesDetected"</c> in v1; future detection
/// reasons (e.g. anti-fraud blocks) would extend this enum.</param>
/// <param name="Candidates">Detected candidates ordered by score descending. Only Tier-1
/// (deterministic) matches are surfaced here — Tier-2 / Tier-3 do not block creation,
/// they're deferred to the recurring background scan.</param>
public sealed record PartyCreateConflictResponse(
    string Reason,
    IReadOnlyList<PartyCreateDuplicateCandidate> Candidates);

/// <summary>
/// One candidate match surfaced in <see cref="PartyCreateConflictResponse"/>. Mirrors
/// <c>DuplicateCandidate</c> in a wire-friendly shape (string tier, primitive score).
/// </summary>
/// <param name="CandidateId">Existing party id that matches the create draft.</param>
/// <param name="Score">Aggregated confidence in <c>[0.0, 1.0]</c> — always <c>1.0</c> at
/// create time since only Tier-1 deterministic matches surface here.</param>
/// <param name="Tier">Detection tier — currently always <c>"Deterministic"</c>.</param>
/// <param name="Signals">Per-signal contributions (e.g. <c>"TaxIdExact"</c>).</param>
public sealed record PartyCreateDuplicateCandidate(
    Guid CandidateId,
    decimal Score,
    string Tier,
    IReadOnlyList<DuplicateMatchSignalResponse> Signals);
