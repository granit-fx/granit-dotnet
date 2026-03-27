using System.Collections.Concurrent;

namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// In-memory implementation of <see cref="IMobilePushTokenReader"/> and <see cref="IMobilePushTokenWriter"/>.
/// Suitable for testing and development. Replaced by EF Core store in production.
/// </summary>
internal sealed class InMemoryMobilePushTokenStore : IMobilePushTokenReader, IMobilePushTokenWriter
{
    private readonly ConcurrentDictionary<string, MobilePushTokenInfo> _tokens = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<MobilePushTokenInfo>> GetTokensAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MobilePushTokenInfo> result = _tokens.Values
            .Where(t => t.UserId == userId && t.TenantId == tenantId)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task RegisterAsync(MobilePushTokenInfo tokenInfo, CancellationToken cancellationToken = default)
    {
        _tokens[tokenInfo.DeviceToken] = tokenInfo;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(deviceToken, out MobilePushTokenInfo? existing) &&
            existing.UserId == userId && existing.TenantId == tenantId)
        {
            _tokens.TryRemove(deviceToken, out _);
        }

        return Task.CompletedTask;
    }
}
