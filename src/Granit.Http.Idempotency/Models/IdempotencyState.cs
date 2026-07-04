namespace Granit.Http.Idempotency.Models;

/// <summary>
/// State of an idempotency entry in Redis.
/// </summary>
public enum IdempotencyState : byte
{
    /// <summary>Request is being executed. Lock held via InProgress TTL.</summary>
    InProgress,

    /// <summary>Request completed. Response stored for replay.</summary>
    Completed,

    /// <summary>
    /// Request completed but its response cannot be replayed (e.g. exceeded
    /// <see cref="IdempotencyOptions.MaxResponseSizeBytes"/>). The original
    /// caller received the response normally; retries with the same key
    /// receive an explicit failure instead of re-executing the business
    /// logic. Preserves the at-most-once idempotency guarantee when the
    /// response itself is not cacheable.
    /// </summary>
    Tombstoned,
}
