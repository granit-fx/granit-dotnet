using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Bulkhead.Options;

/// <summary>
/// Configuration for a single bulkhead policy. Defines per-tenant concurrency and queue limits.
/// </summary>
public sealed class BulkheadPolicyOptions
{
    /// <summary>
    /// Maximum number of concurrent operations per tenant for this policy.
    /// Default: 10. Range: 1–10,000.
    /// </summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// Maximum number of queued operations per tenant when all concurrency slots are occupied.
    /// When the queue is full, requests are immediately rejected with 503 Service Unavailable.
    /// Set to 0 for no queuing (reject immediately). Default: 0. Range: 0–10,000.
    /// </summary>
    [Range(0, 10_000)]
    public int QueueLimit { get; set; }

    /// <summary>
    /// Maximum time a request waits in queue before being rejected.
    /// Only applies when <see cref="QueueLimit"/> &gt; 0. Default: 30 seconds.
    /// </summary>
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional <c>Granit.Features</c> Numeric feature name to resolve <see cref="PermitLimit"/> dynamically.
    /// When <see langword="null"/>, uses the convention <c>Bulkhead.{PolicyName}</c>.
    /// Only used when <see cref="GranitBulkheadOptions.UseFeatureBasedQuotas"/> is <see langword="true"/>.
    /// </summary>
    public string? FeatureName { get; set; }
}
