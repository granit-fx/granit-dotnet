namespace Granit.Http.Idempotency.Abstractions;

/// <summary>
/// Marker contract implemented by <see cref="Attributes.IdempotentAttribute"/> and consumed by
/// <see cref="Internal.IdempotencyMiddleware"/> to detect idempotent endpoints.
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
    /// Use <c>-1</c> to inherit from <see cref="Models.IdempotencyOptions.CompletedTtl"/>.
    /// </summary>
    int CompletedTtlSeconds { get; }
}
