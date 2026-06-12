using Granit.Identity.Models;

namespace Granit.Identity.UserSessions.Internal;

/// <summary>
/// <see cref="IUserDeviceProvider"/> backed by <see cref="IIdentitySessionManager"/>'s device-activity
/// view. The identity provider's SSO devices are browser-based, so every device is classified as
/// <see cref="DeviceKind.Browser"/> — a precise kind requires the OIDC client to declare it (see the
/// "OIDC client declares its device kind" feature).
/// </summary>
internal sealed class IdentityUserDeviceProvider(IIdentitySessionManager sessions) : IUserDeviceProvider
{
    public async Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentityDeviceActivity> devices = await sessions
            .GetUserDeviceActivityAsync(userId, cancellationToken).ConfigureAwait(false);

        return [.. devices.Select(Map)];
    }

    private static UserDevice Map(IdentityDeviceActivity device) =>
        new(
            DeviceId(device),
            DeviceKind.Browser,
            device.OperatingSystem,
            device.Browser,
            device.LastAccess,
            device.Sessions.Count,
            LastLocation: null);

    // The device-activity view has no stable id; synthesize one from its signature so the same device
    // groups consistently across calls. A device-bound identifier is the "Trusted devices" feature's job.
    private static string DeviceId(IdentityDeviceActivity d) =>
        $"{d.Device ?? "?"}/{d.OperatingSystem ?? "?"}/{d.Browser ?? "?"}";
}
