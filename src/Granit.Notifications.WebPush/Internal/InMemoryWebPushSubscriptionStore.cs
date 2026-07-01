using System.Collections.Concurrent;

namespace Granit.Notifications.WebPush.Internal;

/// <summary>In-memory push subscription store for development/testing.</summary>
internal sealed class InMemoryWebPushSubscriptionStore : IWebPushSubscriptionReader, IWebPushSubscriptionWriter
{
    private readonly ConcurrentDictionary<string, List<WebPushSubscriptionInfo>> _subscriptions = new();

    public Task<IReadOnlyList<WebPushSubscriptionInfo>> GetSubscriptionsAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(userId, tenantId);
        IReadOnlyList<WebPushSubscriptionInfo> result = _subscriptions.TryGetValue(key, out List<WebPushSubscriptionInfo>? subs)
            ? subs.ToList()
            : [];
        return Task.FromResult(result);
    }

    public Task SaveSubscriptionAsync(
        string userId, WebPushSubscriptionInfo subscription, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(userId, tenantId);
        _subscriptions.AddOrUpdate(
            key,
            _ => [subscription],
            (_, existing) =>
            {
                existing.RemoveAll(s => s.Endpoint == subscription.Endpoint);
                existing.Add(subscription);
                return existing;
            });
        return Task.CompletedTask;
    }

    public Task RemoveSubscriptionAsync(string endpoint, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        foreach (KeyValuePair<string, List<WebPushSubscriptionInfo>> kvp in _subscriptions)
        {
            kvp.Value.RemoveAll(s => s.Endpoint == endpoint);
        }
        return Task.CompletedTask;
    }

    private static string BuildKey(string userId, Guid? tenantId) =>
        tenantId.HasValue ? $"{tenantId.Value}:{userId}" : userId;
}
