namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Context for a "was this you?" review page, returned by the side-effect-free GET. Lets the page render the
/// prompt and show whether the session has already been reviewed.
/// </summary>
/// <param name="Country">Country code of the flagged sign-in, when resolved.</param>
/// <param name="Decision">The decision already recorded, or <see langword="null"/> when the session is still pending review.</param>
public sealed record SessionReviewContextResponse(
    string? Country,
    UserSessionReviewDecision? Decision);

/// <summary>
/// Body of the review-commit POST. The decision is carried here — never in the token — so a prefetched GET of the
/// email link can never imply a decision.
/// </summary>
/// <param name="Token">The signed review token from the email link.</param>
/// <param name="Decision">The user's answer.</param>
public sealed record SessionReviewDecisionRequest(
    string Token,
    UserSessionReviewDecision Decision);

/// <summary>Result of committing a review decision.</summary>
/// <param name="Decision">The authoritative recorded decision (the first one wins on a repeat call).</param>
/// <param name="Applied">
/// <see langword="true"/> when this call performed the decision's side effects; <see langword="false"/> when the
/// session had already been reviewed and the call was an idempotent no-op.
/// </param>
public sealed record SessionReviewResultResponse(
    UserSessionReviewDecision Decision,
    bool Applied);
