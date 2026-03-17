using Granit.Http.Idempotency.Models;

namespace Granit.Http.Idempotency.Abstractions;

/// <summary>
/// Atomic store for idempotency entries backed by Redis.
/// </summary>
/// <remarks>
/// State machine: <c>Absent → InProgress → Completed</c> (or back to <c>Absent</c> on 5xx/timeout).
/// All operations are atomic at the Redis level (single round-trip via SET NX/XX PX).
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to atomically acquire the <c>InProgress</c> lock for <paramref name="key"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the lock was acquired (state was <c>Absent</c>);
    /// <see langword="false"/> if the key already exists (state is <c>InProgress</c> or
    /// <c>Completed</c>).
    /// </returns>
    Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the stored entry for <paramref name="key"/>, or <see langword="null"/> if absent.
    /// </summary>
    Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions the entry from <c>InProgress</c> to <c>Completed</c> (SET XX PX).
    /// </summary>
    Task SetCompletedAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the entry (transitions from <c>InProgress</c> to <c>Absent</c>).
    /// Called on 5xx responses and execution timeouts.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
