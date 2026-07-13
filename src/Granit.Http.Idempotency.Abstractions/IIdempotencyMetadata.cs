namespace Granit.Http.Idempotency;

/// <summary>
/// Marker contract implemented by <see cref="IdempotentAttribute"/> and consumed by
/// the idempotency middleware to detect idempotent endpoints.
/// </summary>
public interface IIdempotencyMetadata
{
    /// <summary>
    /// When <see langword="true"/>, requests without an <c>Idempotency-Key</c> header are
    /// rejected with HTTP 422. When <see langword="false"/>, the middleware is bypassed.
    /// </summary>
    bool Required { get; }

    /// <summary>
    /// Override for the completed-entry TTL in seconds.
    /// Use <c>-1</c> to inherit from the global <c>IdempotencyOptions.CompletedTtl</c>.
    /// </summary>
    int CompletedTtlSeconds { get; }
}
