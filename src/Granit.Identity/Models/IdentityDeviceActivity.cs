namespace Granit.Identity.Models;

/// <summary>
/// Represents a device from which a user has active sessions.
/// </summary>
/// <remarks>
/// <para>
/// Device-level fields (<see cref="Device"/>, <see cref="OperatingSystem"/>, <see cref="OperatingSystemVersion"/>,
/// <see cref="Browser"/>) are populated when the identity provider supports device-level
/// session tracking (e.g. Keycloak Account API via token exchange).
/// They are <c>null</c> when only session-level data is available.
/// </para>
/// <para>
/// A single device may group multiple <see cref="Sessions"/> (e.g. different browser tabs
/// or applications on the same device).
/// </para>
/// </remarks>
/// <param name="IpAddress">Last known IP address for this device.</param>
/// <param name="LastAccess">Most recent activity timestamp across all sessions on this device.</param>
/// <param name="Device">Device category (e.g. <c>"Desktop"</c>, <c>"Mobile"</c>, <c>"Other"</c>). <c>null</c> if unavailable.</param>
/// <param name="OperatingSystem">Operating system name (e.g. <c>"Windows"</c>, <c>"macOS"</c>, <c>"iOS"</c>). <c>null</c> if unavailable.</param>
/// <param name="OperatingSystemVersion">Operating system version (e.g. <c>"10"</c>, <c>"14"</c>). <c>null</c> if unavailable.</param>
/// <param name="Browser">Browser name and version (e.g. <c>"Chrome/120.0"</c>). <c>null</c> if unavailable.</param>
/// <param name="Mobile">Whether the device is a mobile device.</param>
/// <param name="Current">Whether this device corresponds to the currently authenticated session.</param>
/// <param name="Sessions">Active sessions associated with this device.</param>
public sealed record IdentityDeviceActivity(
    string? IpAddress,
    DateTimeOffset LastAccess,
    string? Device,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? Browser,
    bool Mobile,
    bool Current,
    IReadOnlyList<IdentitySession> Sessions);
