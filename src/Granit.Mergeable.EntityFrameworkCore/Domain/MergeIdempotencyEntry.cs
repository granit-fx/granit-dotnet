namespace Granit.Mergeable.EntityFrameworkCore.Domain;

/// <summary>
/// Cached merge result keyed by an idempotency key. A second call with the same key + same
/// request hash replays the cached result; a different hash with the same key is rejected
/// with a 409. Garbage-collected after 24h by a recurring background job (out of scope of
/// this package — host configures retention).
/// </summary>
internal sealed class MergeIdempotencyEntry
{
    public required Guid Id { get; init; }

    /// <summary>Idempotency key supplied by the caller (HTTP <c>Idempotency-Key</c> header).</summary>
    public required string Key { get; init; }

    /// <summary>SHA-256 of the canonicalised request body (survivorId + loserId + choices).</summary>
    public required string RequestHash { get; init; }

    /// <summary>Survivor id (the merge target).</summary>
    public required Guid SurvivorId { get; init; }

    /// <summary>Loser id (the merge source).</summary>
    public required Guid LoserId { get; init; }

    /// <summary>Serialised <c>MergeResult</c> JSON for replay.</summary>
    public required string ResultJson { get; init; }

    /// <summary>UTC timestamp when the entry was written.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
