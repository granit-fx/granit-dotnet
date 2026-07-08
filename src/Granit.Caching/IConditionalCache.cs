namespace Granit.Caching;

/// <summary>
/// Low-level cache with atomic conditional writes (SET-if-absent / SET-if-present).
/// </summary>
/// <remarks>
/// <para>
/// Complements <c>IFusionCache</c> for scenarios requiring atomic state transitions
/// (e.g., distributed locks, idempotency state machines, compare-and-swap).
/// <c>IFusionCache</c> is the preferred API for standard cache-aside patterns;
/// use <see cref="IConditionalCache"/> only when conditional writes are essential.
/// </para>
/// <para>
/// Two implementations are provided:
/// <list type="bullet">
///   <item><c>InMemoryConditionalCache</c> — development / single-instance (registered by default).</item>
///   <item><c>RedisConditionalCache</c> — production, uses Redis SET NX PX / SET XX PX
///     (registered by <c>Granit.Caching.StackExchangeRedis</c>).</item>
/// </list>
/// </para>
/// <para>
/// Keys are logical: implementations automatically namespace them as
/// <c>{KeyPrefix}:cond:t:{tenantId|host}:{key}</c> (same app prefix and tenant isolation
/// as <c>IFusionCache</c> entries). Callers must NOT re-encode the application namespace
/// or the current tenant into the key; tenant-independent keys require an ambient
/// tenant-free context (the tenant segment is read from <c>ICurrentTenant</c> at call time).
/// </para>
/// </remarks>
public interface IConditionalCache
{
    /// <summary>
    /// Atomically sets the value only if the key does <b>not</b> already exist (SET NX).
    /// </summary>
    /// <typeparam name="T">Value type (must be JSON-serializable for the Redis implementation).</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="ttl">Time-to-live for the entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the value was set (key was absent);
    /// <see langword="false"/> if the key already exists.</returns>
    Task<bool> SetIfAbsentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically sets the value only if the key <b>already exists</b> (SET XX).
    /// </summary>
    /// <typeparam name="T">Value type (must be JSON-serializable for the Redis implementation).</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">New value to store.</param>
    /// <param name="ttl">New time-to-live for the entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the value was updated (key existed);
    /// <see langword="false"/> if the key was absent or expired.</returns>
    Task<bool> SetIfPresentAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the stored value for <paramref name="key"/>, or <see langword="default"/>
    /// if the key is absent or expired.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the entry for <paramref name="key"/>.
    /// No-op if the key is absent.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
