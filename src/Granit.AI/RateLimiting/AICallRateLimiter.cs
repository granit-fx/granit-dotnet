using System.Collections.Concurrent;

namespace Granit.AI.RateLimiting;

/// <summary>
/// In-memory sliding-window <see cref="IAICallRateLimiter"/>. Per-bucket queue of
/// timestamps trimmed on every <see cref="TryAcquireAsync"/> call — memory grows only
/// with the count of distinct buckets active in the trailing hour.
/// </summary>
/// <remarks>
/// Uses <see cref="TimeProvider"/> rather than <see cref="DateTime.UtcNow"/> so tests
/// can freeze and advance the clock deterministically.
/// </remarks>
public sealed class AICallRateLimiter : IAICallRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, BucketWindow> _buckets = new(StringComparer.Ordinal);

    public AICallRateLimiter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public ValueTask<bool> TryAcquireAsync(
        string bucketKey,
        int maxCallsPerHour,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallsPerHour);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = _timeProvider.GetUtcNow();
        BucketWindow bucket = _buckets.GetOrAdd(bucketKey, static _ => new BucketWindow());

        lock (bucket.Lock)
        {
            while (bucket.Timestamps.Count > 0
                && now - bucket.Timestamps.Peek() > Window)
            {
                bucket.Timestamps.Dequeue();
            }

            if (bucket.Timestamps.Count >= maxCallsPerHour)
            {
                return ValueTask.FromResult(false);
            }

            bucket.Timestamps.Enqueue(now);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class BucketWindow
    {
        public Queue<DateTimeOffset> Timestamps { get; } = new();
        public Lock Lock { get; } = new();
    }
}
