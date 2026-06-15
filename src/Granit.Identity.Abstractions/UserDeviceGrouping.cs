namespace Granit.Identity;

/// <summary>
/// Synthesises the <see cref="UserDevice"/> list for a backend that exposes no stable device identity of its
/// own — only a stream of sessions (the BFF token store, the OpenIddict token store). One device is derived per
/// distinct client IP, so the <c>/devices</c> view stays consistent with the <c>/sessions</c> view served by the
/// same backend.
/// </summary>
public static class UserDeviceGrouping
{
    /// <summary>
    /// Groups <paramref name="sessions"/> by client IP and projects one <see cref="UserDevice"/> per group:
    /// the IP is the device signature, last-seen is the latest activity in the group, and the session count is
    /// the group size. The kind is the most specific kind seen across the group's sessions (a declared
    /// <c>MobileApp</c>/<c>Tv</c>/… beats a plain browser), defaulting to <see cref="DeviceKind.Browser"/> —
    /// such a backend's devices are browser-based unless a session declares otherwise. OS, browser and location
    /// are <see langword="null"/>: an IP-only backend does not expose them.
    /// </summary>
    public static IReadOnlyList<UserDevice> ByIpAddress(IReadOnlyList<UserSessionDescriptor> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return
        [
            .. sessions
                .GroupBy(s => s.IpAddress ?? "unknown")
                .Select(g => new UserDevice(
                    DeviceId: g.Key,
                    Kind: ResolveGroupKind(g),
                    OperatingSystem: null,
                    Browser: null,
                    LastSeen: g.Max(s => s.LastAccessedAt),
                    SessionCount: g.Count(),
                    LastLocation: null)),
        ];
    }

    private static DeviceKind ResolveGroupKind(IEnumerable<UserSessionDescriptor> group) =>
        group.Select(s => s.Kind)
            .FirstOrDefault(kind => kind is not DeviceKind.Unknown and not DeviceKind.Browser, DeviceKind.Browser);
}
