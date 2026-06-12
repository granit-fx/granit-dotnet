namespace Granit.Identity.Internal;

/// <summary>
/// No-op <see cref="IUserDeviceProvider"/> registered by default: reports no devices. Replaced when a
/// backend integration package is installed.
/// </summary>
internal sealed class NullUserDeviceProvider : IUserDeviceProvider
{
    public Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserDevice>>([]);
}
