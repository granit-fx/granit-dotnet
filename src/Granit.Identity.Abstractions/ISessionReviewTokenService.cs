namespace Granit.Identity;

/// <summary>
/// Decoded payload of a session-review action token: the subject, the flagged session, and the device the
/// session was bound to (when any). The decision (confirm/deny) is deliberately <b>not</b> part of the token —
/// it is the body of an explicit POST — so a prefetched GET of the email link can never imply a decision.
/// </summary>
/// <param name="UserId">Subject the review is for; re-checked on use.</param>
/// <param name="SessionId">The flagged session under review.</param>
/// <param name="DeviceId">Device the session was bound to, when any — trusted on a "yes" answer.</param>
/// <param name="Country">Country code of the flagged sign-in, when resolved — reinforced into the habitual profile on a "yes" answer.</param>
public sealed record SessionReviewTokenPayload(string UserId, string SessionId, string? DeviceId, string? Country);

/// <summary>
/// Issues and validates the signed, time-limited token carried in a "was this you?" email link. The contract
/// lives in <c>Granit.Identity.Abstractions</c> so the notification layer can mint a link and the endpoints
/// layer can validate it without either depending on the other; the data-protected implementation ships with
/// <c>Granit.Identity.Endpoints</c>. When no implementation is registered (no endpoints package), the alert
/// simply carries no review link.
/// </summary>
public interface ISessionReviewTokenService
{
    /// <summary>Mints a time-limited, encrypted review token for the given subject/session/device/country.</summary>
    string Issue(string userId, string sessionId, string? deviceId, string? country);

    /// <summary>
    /// Validates and decodes a token, or returns <see langword="null"/> when it is malformed, tampered with, or
    /// expired.
    /// </summary>
    SessionReviewTokenPayload? Validate(string token);
}
