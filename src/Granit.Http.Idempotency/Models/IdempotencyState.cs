namespace Granit.Http.Idempotency.Models;

/// <summary>
/// State of an idempotency entry in Redis.
/// </summary>
public enum IdempotencyState : byte
{
    /// <summary>Request is being executed. Lock held via InProgress TTL.</summary>
    InProgress = 0,

    /// <summary>Request completed. Response stored for replay.</summary>
    Completed = 1,
}
