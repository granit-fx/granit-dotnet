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
    /// TTL for tombstone entries (responses that ran successfully but cannot
    /// be replayed, e.g. oversized). Default: 24 hours — matches
    /// <see cref="CompletedTtl"/> so a client that retries within the
    /// normal window sees a deterministic 413 instead of re-executing.
    /// </summary>
    public TimeSpan TombstoneTtl { get; set; } = TimeSpan.FromHours(24);

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
    /// Maximum response body size stored for replay. Default: 256 KiB.
    /// When the captured response exceeds this limit, the client still
    /// receives the full response, but the entry is written as a
    /// tombstone (<see cref="IdempotencyState.Tombstoned"/>) so retries
    /// return HTTP 413 instead of re-executing the handler.
    /// </summary>
    public int MaxResponseSizeBytes { get; set; } = 256 * 1024;

    /// <summary>
    /// Maximum length of the client-supplied idempotency key header value.
    /// Values longer than this limit are rejected with HTTP 400 before any
    /// hashing or cache lookup. Default: 256 — accommodates a UUIDv4 plus
    /// a tenant/correlation prefix.
    /// </summary>
    public int MaxKeyLength { get; set; } = 256;

    /// <summary>
    /// Predicate that controls which HTTP status codes are cached for replay.
    /// Default: 2xx + {400, 404, 409, 410, 422}. 401/403 are never cached.
    /// </summary>
    public Func<int, bool> ShouldCacheStatusCode { get; set; } =
        static sc => sc is (>= 200 and < 300) or 400 or 404 or 409 or 410 or 422;

    /// <summary>
    /// Response headers that must NOT be captured during execution and NOT
    /// re-emitted during replay. Protects against re-issuing per-request
    /// security artefacts (CSRF tokens, rotating session cookies,
    /// authentication challenges) when the cached response is replayed for
    /// a later retry.
    /// </summary>
    /// <remarks>
    /// The set uses case-insensitive comparison. Additional entries can be
    /// added via options configuration:
    /// <code>
    /// services.Configure&lt;IdempotencyOptions&gt;(opts =&gt;
    ///     opts.ExcludedResponseHeaders.Add("X-Custom-Session-Token"));
    /// </code>
    /// </remarks>
    public HashSet<string> ExcludedResponseHeaders { get; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Set-Cookie",
            "Set-Cookie2",
            "WWW-Authenticate",
            "Proxy-Authenticate",
            "Authorization",
            "Server",
            "Date",
            "Transfer-Encoding",
        };
}
