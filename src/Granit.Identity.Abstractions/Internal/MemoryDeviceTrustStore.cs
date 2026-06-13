using System.Collections.Concurrent;

namespace Granit.Identity.Internal;

/// <summary>
/// In-memory, single-node, non-durable <see cref="IDeviceTrustStore"/> registered by default. Suitable for
/// development and tests; replaced by <c>Granit.Identity.EntityFrameworkCore</c> for durable production use.
/// </summary>
internal sealed class MemoryDeviceTrustStore : IDeviceTrustStore
{
    private readonly ConcurrentDictionary<(string UserId, string DeviceId), DeviceTrustVerdict> _verdicts =
        new();

    public Task SetAsync(
        string userId,
        string deviceId,
        DeviceTrustVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        _verdicts[(userId, deviceId)] = verdict;
        return Task.CompletedTask;
    }

    public Task<DeviceTrustVerdict?> GetAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_verdicts.GetValueOrDefault((userId, deviceId)));

    public Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, DeviceTrustVerdict> result = [];
        foreach (string deviceId in deviceIds)
        {
            if (_verdicts.TryGetValue((userId, deviceId), out DeviceTrustVerdict? verdict))
            {
                result[deviceId] = verdict;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, DeviceTrustVerdict>>(result);
    }

    public Task RevokeAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        _verdicts.TryRemove((userId, deviceId), out _);
        return Task.CompletedTask;
    }
}
