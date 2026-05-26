namespace Granit.Indexing.Internal;

/// <summary>
/// Sliding-window rate limiter for empty search results, keyed by principal identifier.
/// Slows down existence-oracle probing on the search surface.
/// </summary>
/// <remarks>
/// Default implementation lives in-process (<see cref="EmptyResultRateLimiter"/>) and is
/// suitable for single-node deployments. Multi-node deployments should replace it with
/// a Redis-backed implementation to share state across instances.
/// </remarks>
internal interface IEmptyResultRateLimiter
{
    /// <summary>
    /// Records an empty-result event for <paramref name="principalIdentifier"/> and
    /// returns <c>true</c> when the principal has exceeded the configured cap within
    /// the last minute.
    /// </summary>
    /// <param name="principalIdentifier">Caller-supplied identifier (already-hashed or raw).</param>
    /// <returns><c>true</c> when the next empty result must be throttled.</returns>
    bool RecordEmptyResultAndShouldThrottle(string principalIdentifier);
}
