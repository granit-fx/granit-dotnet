using Granit.IpGeolocation;

namespace Granit.UserSessions;

/// <summary>
/// Canonical, store-agnostic view of a device a user has signed in from — an aggregation of one
/// or more sessions sharing the same device fingerprint.
/// </summary>
/// <remarks>
/// Surfaced by the canonical <c>/devices</c> API. Like <see cref="UserSessionDescriptor"/>, every
/// derived field is optional: a backend populates only what its data exposes. The raw client IP is
/// never carried here — only the privacy-friendlier <see cref="LastLocation"/>.
/// </remarks>
/// <param name="DeviceId">Stable device identifier (fingerprint or backend device id).</param>
/// <param name="DeviceType">Coarse form factor (e.g. <c>"desktop"</c>, <c>"mobile"</c>, <c>"tablet"</c>), when known.</param>
/// <param name="OperatingSystem">Operating-system family (e.g. <c>"Windows"</c>), when known.</param>
/// <param name="Browser">Browser family (e.g. <c>"Chrome"</c>), when known.</param>
/// <param name="LastSeen">Last observed activity from this device, when tracked.</param>
/// <param name="SessionCount">Number of active sessions associated with this device.</param>
/// <param name="LastLocation">Approximate location of the most recent activity, when resolved.</param>
public sealed record UserDevice(
    string DeviceId,
    string? DeviceType,
    string? OperatingSystem,
    string? Browser,
    DateTimeOffset? LastSeen,
    int SessionCount,
    GeoLocation? LastLocation);
