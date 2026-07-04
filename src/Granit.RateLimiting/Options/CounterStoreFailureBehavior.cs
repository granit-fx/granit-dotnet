namespace Granit.RateLimiting.Options;

/// <summary>
/// Behavior when the counter store (Redis) is unavailable.
/// </summary>
public enum CounterStoreFailureBehavior : byte
{
    /// <summary>
    /// Open degradation: allow the request and log a warning.
    /// Prevents a Redis outage from causing complete service unavailability.
    /// </summary>
    Allow,

    /// <summary>
    /// Closed degradation: reject the request with 429.
    /// Conservative approach — prefer availability loss over quota bypass.
    /// </summary>
    Deny,
}
