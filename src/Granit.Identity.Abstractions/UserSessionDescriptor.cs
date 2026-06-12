using Granit.IpGeolocation;

namespace Granit.Identity;

/// <summary>
/// Canonical, store-agnostic description of a single user session.
/// </summary>
/// <remarks>
/// Both session layers project to this shape — the BFF gateway store and the identity authority view — so a
/// single enrichment pipeline (geolocation, last-activity) and the anomaly detector operate uniformly across
/// them. <see cref="IpAddress"/> is the raw server-side value (personal data — never log it in clear, and mask
/// it before exposing to a browser); <see cref="Location"/> is the privacy-friendlier derived form.
/// </remarks>
/// <param name="SessionId">Opaque session identifier (already masked when surfaced to end users).</param>
/// <param name="UserId">Subject the session belongs to, when known.</param>
/// <param name="IsCurrent">Whether this is the caller's current session.</param>
/// <param name="CreatedAt">When the session was established.</param>
/// <param name="LastAccessedAt">Last observed activity, when tracked.</param>
/// <param name="UserAgent">User-Agent captured at establishment, when available.</param>
/// <param name="IpAddress">Raw client IP (server-side only), when available.</param>
/// <param name="Location">Approximate location derived from <paramref name="IpAddress"/>, when resolved.</param>
public sealed record UserSessionDescriptor(
    string SessionId,
    string? UserId,
    bool IsCurrent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAccessedAt,
    string? UserAgent,
    string? IpAddress,
    GeoLocation? Location);
