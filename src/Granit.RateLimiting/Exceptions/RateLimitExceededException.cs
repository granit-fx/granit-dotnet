using Granit.Exceptions;

namespace Granit.RateLimiting.Exceptions;

/// <summary>
/// Thrown when a rate limit quota is exceeded. Mapped to HTTP 429 Too Many Requests.
/// </summary>
public sealed class RateLimitExceededException : BusinessException
{
    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitExceededException"/>.
    /// </summary>
    /// <param name="policyName">Name of the rate limiting policy that was exceeded.</param>
    /// <param name="retryAfter">Time to wait before retrying.</param>
    /// <param name="limit">Total permit limit for the policy.</param>
    /// <param name="remaining">Number of permits remaining (typically 0).</param>
    public RateLimitExceededException(string policyName, TimeSpan retryAfter, int limit, int remaining)
        : base($"Rate limit exceeded for policy '{policyName}'. Retry after {retryAfter.TotalSeconds:F0}s.")
    {
        PolicyName = policyName;
        RetryAfter = retryAfter;
        Limit = limit;
        Remaining = remaining;
    }

    /// <summary>Name of the rate limiting policy.</summary>
    public string PolicyName { get; }

    /// <summary>Time to wait before retrying.</summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>Total permit limit for the policy.</summary>
    public int Limit { get; }

    /// <summary>Number of permits remaining.</summary>
    public int Remaining { get; }
}
