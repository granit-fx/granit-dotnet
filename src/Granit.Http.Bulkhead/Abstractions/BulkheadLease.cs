using System.Threading.RateLimiting;

namespace Granit.Http.Bulkhead.Abstractions;

/// <summary>
/// Represents a concurrency permit acquired from a bulkhead.
/// Disposing the lease releases the permit back to the <see cref="ConcurrencyLimiter"/>.
/// </summary>
public sealed class BulkheadLease : IDisposable
{
    /// <summary>Singleton no-op lease used when bulkhead is disabled or bypassed.</summary>
    public static readonly BulkheadLease NoOp = new(null, null);

    private RateLimitLease? _innerLease;
    private Action? _onDispose;
    private int _disposed;

    internal BulkheadLease(RateLimitLease? innerLease, Action? onDispose)
    {
        _innerLease = innerLease;
        _onDispose = onDispose;
        IsAcquired = innerLease?.IsAcquired ?? true;
    }

    /// <summary>Whether the permit was successfully acquired.</summary>
    public bool IsAcquired { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _innerLease?.Dispose();
            _innerLease = null;
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }
}
