namespace Granit.Http.Idempotency.Models;

/// <summary>
/// Represents a stored idempotency entry (serialized to Redis).
/// </summary>
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
}
