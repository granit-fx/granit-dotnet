namespace Granit.RateLimiting.Options;

/// <summary>
/// Rate limiting algorithm to apply for a given policy.
/// </summary>
public enum RateLimitAlgorithm : byte
{
    /// <summary>Sliding window with configurable segments. Most accurate, moderate memory.</summary>
    SlidingWindow,

    /// <summary>Fixed window with single counter. Lightest but subject to burst at window edges.</summary>
    FixedWindow,

    /// <summary>Token bucket with configurable refill rate. Best for controlled burst allowance.</summary>
    TokenBucket,

    /// <summary>Concurrency limiter. Limits simultaneous in-flight requests, not rate.</summary>
    Concurrency,
}
