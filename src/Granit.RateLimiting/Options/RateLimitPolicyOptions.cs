using System.ComponentModel.DataAnnotations;

namespace Granit.RateLimiting.Options;

/// <summary>
/// Configuration for a single rate limiting policy.
/// </summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>Algorithm to use. Default: <see cref="RateLimitAlgorithm.SlidingWindow"/>.</summary>
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;

    /// <summary>Key partitioning strategy. Default: <see cref="RateLimitPartition.Tenant"/>.</summary>
    public RateLimitPartition PartitionBy { get; set; } = RateLimitPartition.Tenant;

    /// <summary>Maximum number of permits in the window. Required for all algorithms except <see cref="RateLimitAlgorithm.TokenBucket"/>.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 1000;

    /// <summary>Time window for sliding and fixed window algorithms. Default: 1 minute.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Number of segments per sliding window. Higher values = more accuracy, more memory. Default: 6.</summary>
    [Range(1, 60)]
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Maximum number of tokens for <see cref="RateLimitAlgorithm.TokenBucket"/>. Default: 50.</summary>
    [Range(1, int.MaxValue)]
    public int TokenLimit { get; set; } = 50;

    /// <summary>Number of tokens added per replenishment period. Default: 10.</summary>
    [Range(1, int.MaxValue)]
    public int TokensPerPeriod { get; set; } = 10;

    /// <summary>Interval between token replenishments. Default: 10 seconds.</summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Optional feature name override for plan-based quota resolution.
    /// When <see langword="null"/>, the convention <c>RateLimit.{PolicyName}</c> is used.
    /// </summary>
    public string? FeatureName { get; set; }
}
