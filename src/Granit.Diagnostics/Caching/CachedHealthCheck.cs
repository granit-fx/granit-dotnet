using Granit.Timing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Diagnostics.Caching;

/// <summary>
/// Decorator that caches the result of an <see cref="IHealthCheck"/> for a configurable duration,
/// using a <see cref="SemaphoreSlim"/> with double-check locking to prevent cache stampede.
/// </summary>
/// <remarks>
/// With 50 pods probed every 3 seconds, an uncached dependency check generates ~16 req/s
/// on the database. This decorator reduces that to 1 req per <paramref name="cacheDuration"/>
/// per pod, regardless of concurrent probe requests.
/// </remarks>
public sealed class CachedHealthCheck(IHealthCheck inner, TimeSpan cacheDuration, IClock clock) : IHealthCheck, IDisposable
{
    private readonly IHealthCheck _inner = inner;
    private readonly TimeSpan _cacheDuration = cacheDuration;
    private readonly IClock _clock = clock;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private HealthCheckResult? _cached;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Fast path — no lock acquisition if cache is warm
        DateTimeOffset now = _clock.Now;
        if (_cached.HasValue && now < _expiresAt)
        {
            return _cached.Value;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock: another thread may have populated the cache
            now = _clock.Now;
            if (_cached.HasValue && now < _expiresAt)
            {
                return _cached.Value;
            }

            HealthCheckResult result = await _inner.CheckHealthAsync(context, cancellationToken).ConfigureAwait(false);
            _cached = result;
            _expiresAt = now.Add(_cacheDuration);
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();
}
