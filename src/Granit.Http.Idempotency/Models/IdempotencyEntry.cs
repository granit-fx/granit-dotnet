using Granit.Caching;

namespace Granit.Http.Idempotency.Models;

/// <summary>
/// Represents a stored idempotency entry (serialized to Redis).
/// </summary>
/// <remarks>
/// SECURITY: <see cref="CacheEncryptedAttribute"/> forces AES-256-GCM encryption of the
/// captured response before it is written to the L2 (Redis) store, independently of the global
/// <see cref="Granit.Caching.Options.CachingOptions.EncryptValues"/> flag. A completed entry
/// replays the original status code, headers, and full response body — any of which may carry
/// PII or bearer tokens — so it must never sit in plaintext in a Redis snapshot, AOF file, or
/// behind an ACL bypass. The attribute is honoured by the store's serializer path
/// (<c>RedisConditionalCache</c> resolves it via <c>CacheEncryptionResolver</c>) and is a no-op
/// on the L1 in-memory store, which holds live .NET object graphs rather than serialized bytes —
/// same threat model as <c>IMemoryCache</c>. Set-Cookie and WWW-Authenticate are already stripped
/// upstream by the middleware, so a rotated cookie or auth challenge is never captured to begin with.
/// </remarks>
[CacheEncrypted]
public sealed record IdempotencyEntry
{
    /// <summary>Current state of the entry.</summary>
    public required IdempotencyState State { get; init; }

    /// <summary>SHA-256 hex digest of the composite key (method + route + idempotency-key value).</summary>
    public required string PayloadHash { get; init; }

    /// <summary>UTC timestamp when the entry was first created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>HTTP status code of the completed response. <see langword="null"/> while InProgress.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Response headers to replay. <see langword="null"/> while InProgress.</summary>
    public Dictionary<string, string[]>? ResponseHeaders { get; init; }

    /// <summary>Response body bytes (Base64 in JSON). <see langword="null"/> while InProgress.</summary>
    public byte[]? ResponseBody { get; init; }

    /// <summary>UTC timestamp when the entry transitioned to Completed. <see langword="null"/> while InProgress.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Reason the entry was tombstoned. Non-<see langword="null"/> only when
    /// <see cref="State"/> is <see cref="IdempotencyState.Tombstoned"/>.
    /// </summary>
    public IdempotencyTombstoneReason? TombstoneReason { get; init; }
}
