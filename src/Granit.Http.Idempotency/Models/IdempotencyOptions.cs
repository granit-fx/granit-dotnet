using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Idempotency.Models;

/// <summary>
/// Configuration options for <see cref="Internal.IdempotencyMiddleware"/>.
/// Bound from <c>appsettings.json</c> section <c>"Idempotency"</c>.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Idempotency";

    /// <summary>Name of the HTTP header carrying the idempotency key. Default: <c>Idempotency-Key</c>.</summary>
    [Required]
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>Redis key prefix. Default: <c>idp</c>.</summary>
    [Required]
    public string KeyPrefix { get; set; } = "idp";

    /// <summary>TTL for completed entries. Default: 24 hours.</summary>
    public TimeSpan CompletedTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// TTL for the InProgress lock. Must be strictly greater than <see cref="ExecutionTimeout"/>.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan InProgressTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum allowed execution time for the downstream handler.
    /// Must be strictly less than <see cref="InProgressTtl"/>.
    /// Default: 25 seconds.
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(25);

    /// <summary>Maximum request body size to hash. Default: 1 MiB.</summary>
    public int MaxBodySizeBytes { get; set; } = 1 * 1024 * 1024;

    /// <summary>
    /// Predicate that controls which HTTP status codes are cached for replay.
    /// Default: 2xx + {400, 404, 409, 410, 422}. 401/403 are never cached.
    /// </summary>
    public Func<int, bool> ShouldCacheStatusCode { get; set; } =
        static sc => sc is (>= 200 and < 300) or 400 or 404 or 409 or 410 or 422;
}
