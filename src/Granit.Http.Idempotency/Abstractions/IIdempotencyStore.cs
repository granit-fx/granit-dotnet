using Granit.Http.Idempotency.Models;

namespace Granit.Http.Idempotency.Abstractions;

/// <summary>
/// Atomic store for idempotency entries.
/// </summary>
/// <remarks>
/// <para>
/// The contract expresses the middleware's state machine explicitly:
/// <c>Absent → InProgress</c> (<see cref="TryAcquireAsync"/>), then
/// <c>InProgress → Completed</c> (<see cref="CompleteAsync"/>) or
/// <c>InProgress → Tombstoned</c> (<see cref="TombstoneAsync"/>), or back to
/// <c>Absent</c> on 5xx/timeout (<see cref="DeleteAsync"/>). Every transition must be
/// atomic with respect to concurrent callers on the same key: acquire is
/// create-if-absent, complete/tombstone are set-if-present.
/// </para>
/// <para>
/// An existence guard is sufficient for <see cref="CompleteAsync"/> and
/// <see cref="TombstoneAsync"/> — no state inspection is needed. Only the request that
/// won <see cref="TryAcquireAsync"/> ever writes the terminal state, and the options
/// validator enforces <c>ExecutionTimeout &lt; InProgressTtl</c>, so the owner always
/// writes before its lock can expire. A <see langword="false"/> return surfaces the
/// residual TTL race (key expired before the write) for logging.
/// </para>
/// <para>
/// Keys arrive fully namespaced from the middleware
/// (<c>{prefix}:{tenant}:{user}:{method}:{route}:{keyHash}</c>). Implementations MUST NOT
/// re-namespace by tenant — doing so would double the tenant segment and break replay.
/// A store-wide instance prefix (multiple applications sharing one backend) is fine.
/// </para>
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Whether entries are shared across application replicas (e.g. Redis) rather than held
    /// per process. The startup guard refuses to run a non-distributed store outside
    /// Development unless <see cref="IdempotencyOptions.AllowInMemoryStore"/> is set,
    /// because a per-pod store silently breaks the at-most-once guarantee under replicas.
    /// </summary>
    bool IsDistributed { get; }

    /// <summary>Human-readable backend name for the startup log (e.g. <c>"RedisIdempotencyStore"</c>).</summary>
    string BackendName { get; }

    /// <summary>
    /// Atomically creates the <c>InProgress</c> entry for <paramref name="key"/> if — and only
    /// if — the key is absent (Redis: <c>SET NX PX</c>).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the lock was acquired (state was <c>Absent</c>);
    /// <see langword="false"/> if the key already exists (state is <c>InProgress</c>,
    /// <c>Completed</c>, or <c>Tombstoned</c>).
    /// </returns>
    Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the stored entry for <paramref name="key"/>, or <see langword="null"/> if absent.
    /// </summary>
    Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions the entry to <c>Completed</c> if — and only if — the key still
    /// exists (Redis: <c>SET XX PX</c>). Never creates the key: a completed response written
    /// after the <c>InProgress</c> lock expired would resurrect a key another replica may
    /// have re-acquired.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the entry was written; <see langword="false"/> if the key no
    /// longer exists (the <c>InProgress</c> TTL expired before the response completed).
    /// </returns>
    Task<bool> CompleteAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions the entry to <c>Tombstoned</c> if — and only if — the key still
    /// exists (Redis: <c>SET XX PX</c>). Tombstones mark a request that executed successfully
    /// once but whose response is not replayable (e.g. exceeded the replay size limit).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the entry was written; <see langword="false"/> if the key no
    /// longer exists.
    /// </returns>
    Task<bool> TombstoneAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the entry (transitions from <c>InProgress</c> to <c>Absent</c>).
    /// Called on 5xx responses and execution timeouts.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
