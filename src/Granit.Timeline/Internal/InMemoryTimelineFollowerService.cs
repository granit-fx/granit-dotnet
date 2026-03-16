using System.Collections.Concurrent;
using Granit.Timeline.Abstractions;

namespace Granit.Timeline.Internal;

/// <summary>
/// Standalone in-memory implementation of <see cref="ITimelineFollowerService"/>.
/// Used when <c>Granit.Timeline.Notifications</c> is not installed.
/// </summary>
internal sealed class InMemoryTimelineFollowerService : ITimelineFollowerService
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _followers = new();

    /// <inheritdoc/>
    public Task FollowAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(entityType, entityId);
        HashSet<string> followers = _followers.GetOrAdd(key, _ => []);

        lock (_lock)
        {
            followers.Add(userId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnfollowAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(entityType, entityId);

        if (_followers.TryGetValue(key, out HashSet<string>? followers))
        {
            lock (_lock)
            {
                followers.Remove(userId);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetFollowerIdsAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(entityType, entityId);

        if (_followers.TryGetValue(key, out HashSet<string>? followers))
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<string>>([.. followers]);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc/>
    public Task<bool> IsFollowingAsync(string userId, string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        string key = BuildKey(entityType, entityId);

        if (_followers.TryGetValue(key, out HashSet<string>? followers))
        {
            lock (_lock)
            {
                return Task.FromResult(followers.Contains(userId));
            }
        }

        return Task.FromResult(false);
    }

    private static string BuildKey(string entityType, string entityId) =>
        $"{entityType}:{entityId}";
}
