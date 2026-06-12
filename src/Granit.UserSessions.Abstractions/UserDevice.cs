using Granit.IpGeolocation;

namespace Granit.UserSessions;

/// <summary>
/// Canonical, store-agnostic view of a device a user has signed in from — an aggregation of one
/// or more sessions sharing the same device identity.
/// </summary>
/// <remarks>
/// Surfaced by the canonical <c>/devices</c> API. <see cref="Kind"/> classifies the client from the
/// authentication context (browser vs native app vs TV vs API client) — distinct from the User-Agent
/// form factor. Every other derived field is optional: a backend populates only what its data exposes.
/// The raw client IP is never carried here — only the privacy-friendlier <see cref="LastLocation"/>.
/// </remarks>
/// <param name="DeviceId">Stable device identifier (device-bound credential, install id, or backend device id).</param>
/// <param name="Kind">The client kind, from the auth context (browser, mobile app, desktop app, TV, API client).</param>
/// <param name="DisplayName">Human-friendly label (e.g. <c>"Chrome on Windows"</c>, <c>"MyApp on iPhone"</c>), when derivable.</param>
/// <param name="OperatingSystem">Operating-system family (e.g. <c>"Windows"</c>), when known.</param>
/// <param name="Browser">Browser family (e.g. <c>"Chrome"</c>) for <see cref="DeviceKind.Browser"/> devices; otherwise <see langword="null"/>.</param>
/// <param name="LastSeen">Last observed activity from this device, when tracked.</param>
/// <param name="SessionCount">Number of active sessions associated with this device.</param>
/// <param name="LastLocation">Approximate location of the most recent activity, when resolved.</param>
public sealed record UserDevice(
    string DeviceId,
    DeviceKind Kind,
    string? DisplayName,
    string? OperatingSystem,
    string? Browser,
    DateTimeOffset? LastSeen,
    int SessionCount,
    GeoLocation? LastLocation);
