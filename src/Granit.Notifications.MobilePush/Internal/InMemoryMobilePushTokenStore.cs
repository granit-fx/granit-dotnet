using System.Collections.Concurrent;
using Granit.Notifications.MobilePush.Domain;

namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// In-memory implementation of <see cref="IMobilePushTokenReader"/> and
/// <see cref="IMobilePushTokenWriter"/>. Suitable for testing and development;
/// replaced by the EF Core store in production.
/// </summary>
internal sealed class InMemoryMobilePushTokenStore(IMobilePushTokenHasher hasher)
    : IMobilePushTokenReader, IMobilePushTokenWriter
{
    private readonly ConcurrentDictionary<(string Hash, Guid? TenantId), MobilePushToken> _tokens = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<MobilePushToken>> GetTokensAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MobilePushToken> result = _tokens.Values
            .Where(t => t.UserId == userId && t.TenantId == tenantId)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task RegisterAsync(
        string userId,
        string deviceToken,
        MobilePlatform platform,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = hasher.ComputeHash(deviceToken)
            ?? throw new ArgumentException("DeviceToken cannot be null or empty.", nameof(deviceToken));

        (string Hash, Guid? TenantId) key = (tokenHash, tenantId);

        if (_tokens.TryGetValue(key, out MobilePushToken? existing))
        {
            existing.Reassign(userId, platform);
            return Task.CompletedTask;
        }

        _tokens[key] = MobilePushToken.Create(userId, deviceToken, tokenHash, platform, tenantId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(
        string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        string? tokenHash = hasher.ComputeHash(deviceToken);
        if (tokenHash is null)
        {
            return Task.CompletedTask;
        }

        (string Hash, Guid? TenantId) key = (tokenHash, tenantId);
        if (_tokens.TryGetValue(key, out MobilePushToken? existing) && existing.UserId == userId)
        {
            _tokens.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
