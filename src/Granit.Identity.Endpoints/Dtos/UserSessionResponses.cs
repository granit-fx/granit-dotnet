using Granit.IpGeolocation;

namespace Granit.Identity.Endpoints.Dtos;

/// <summary>A single session in the canonical session list. The IP is masked by default (host portion zeroed); a deployment may opt in to the raw IP via <c>ExposeRawIpAddress</c>.</summary>
/// <param name="SessionId">Opaque session identifier.</param>
/// <param name="IsCurrent">Whether this is the caller's current session.</param>
/// <param name="CreatedAt">When the session was established.</param>
/// <param name="LastAccessedAt">Last observed activity, when tracked.</param>
/// <param name="UserAgent">User-Agent captured at establishment, when available.</param>
/// <param name="IpAddress">Client IP, masked to its network portion by default (GDPR data minimisation); the raw value only when the deployment opts in. <see langword="null"/> when not captured.</param>
/// <param name="Location">Approximate location, when resolved.</param>
/// <param name="RiskLevel">Persisted risk classification, when assessed.</param>
/// <param name="RiskReasons">Machine-readable reason codes behind the risk level, when assessed.</param>
public sealed record UserSessionResponse(
    string SessionId,
    bool IsCurrent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAccessedAt,
    string? UserAgent,
    string? IpAddress,
    GeoLocation? Location,
    UserSessionRiskLevel? RiskLevel,
    IReadOnlyList<string>? RiskReasons);

/// <summary>A device a user has signed in from.</summary>
/// <param name="DeviceId">Stable device identifier.</param>
/// <param name="Kind">The client kind (browser, mobile app, desktop app, TV, API client).</param>
/// <param name="OperatingSystem">Operating-system family, when known.</param>
/// <param name="Browser">Browser family for browser devices; otherwise null. The client composes a localized device label from this and <paramref name="OperatingSystem"/>.</param>
/// <param name="LastSeen">Last observed activity from this device, when tracked.</param>
/// <param name="SessionCount">Number of active sessions on this device.</param>
/// <param name="LastLocation">Approximate location of the most recent activity, when resolved.</param>
/// <param name="IsTrusted">Whether the user has marked this device trusted and the trust is still active.</param>
/// <param name="TrustedUntil">When the active trust expires, when bounded; <see langword="null"/> when not trusted or open-ended.</param>
public sealed record UserDeviceResponse(
    string DeviceId,
    DeviceKind Kind,
    string? OperatingSystem,
    string? Browser,
    DateTimeOffset? LastSeen,
    int SessionCount,
    GeoLocation? LastLocation,
    bool IsTrusted,
    DateTimeOffset? TrustedUntil);

/// <summary>Result of revoking every session except the caller's current one.</summary>
/// <param name="RevokedCount">Number of sessions revoked.</param>
public sealed record UserSessionsRevokedResponse(int RevokedCount);

/// <summary>Result of marking the current device trusted.</summary>
/// <param name="DeviceId">The stable device identifier the current browser is now bound to and trusted as.</param>
/// <param name="TrustedUntil">When the trust expires.</param>
public sealed record DeviceTrustedResponse(string DeviceId, DateTimeOffset TrustedUntil);
