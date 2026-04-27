using System.Collections.Concurrent;
using Granit.Identity.Federated.Options;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.RateLimiting;

/// <summary>
/// Process-local <see cref="IUserSyncFailureRateLimiter"/> backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>. Lets through one emission
/// per (<c>UserId</c>, <c>ProviderName</c>) per
/// <see cref="IdentityFederatedNotificationOptions.SyncFailureCoolOffMinutes"/>
/// window. Stale entries are purged opportunistically on each
/// <see cref="TryAcquire"/> call so the dictionary stays bounded by the active
/// failure population.
/// </summary>
/// <remarks>
/// In-process only — adequate for single-pod deployments where the framework
/// stays dependency-free. Multi-pod hosts should override the DI registration
/// with a <c>Granit.RateLimiting</c>-backed limiter so the cool-off is enforced
/// cluster-wide; otherwise an attacker can cycle through pods to multiply the
/// effective notification rate.
/// </remarks>
public sealed class InMemoryUserSyncFailureRateLimiter : IUserSyncFailureRateLimiter
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _expiresAt = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<IdentityFederatedNotificationOptions> _options;

    public InMemoryUserSyncFailureRateLimiter(
        TimeProvider timeProvider,
        IOptionsMonitor<IdentityFederatedNotificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <inheritdoc />
    public bool TryAcquire(string userId, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromMinutes(Math.Max(1, _options.CurrentValue.SyncFailureCoolOffMinutes));
        DateTimeOffset newExpiry = now + window;
        string key = $"{providerName}|{userId}";

        // Opportunistic cleanup so the dictionary doesn't grow unbounded.
        // Only scans when the dictionary is non-trivial to avoid O(n) on hot paths.
        if (_expiresAt.Count > 64)
        {
            foreach (KeyValuePair<string, DateTimeOffset> kvp in _expiresAt)
            {
                if (kvp.Value <= now)
                {
                    _expiresAt.TryRemove(kvp);
                }
            }
        }

        bool acquired = false;
        _expiresAt.AddOrUpdate(
            key,
            _ =>
            {
                acquired = true;
                return newExpiry;
            },
            (_, existing) =>
            {
                if (existing <= now)
                {
                    acquired = true;
                    return newExpiry;
                }

                acquired = false;
                return existing;
            });

        return acquired;
    }
}
