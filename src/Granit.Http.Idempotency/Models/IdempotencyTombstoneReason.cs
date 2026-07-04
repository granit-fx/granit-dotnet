namespace Granit.Http.Idempotency.Models;

/// <summary>
/// Reason a completed idempotency entry was tombstoned instead of fully cached.
/// </summary>
/// <remarks>
/// Tombstones record that the request ran successfully once but its response
/// is not replayable. The enum is extensible so new cases can be added
/// (non-deterministic response, explicit opt-out, etc.) without changing the
/// shape of <see cref="IdempotencyEntry"/>.
/// </remarks>
public enum IdempotencyTombstoneReason : byte
{
    /// <summary>
    /// Response body exceeded <see cref="IdempotencyOptions.MaxResponseSizeBytes"/>.
    /// Replays return HTTP 413 Payload Too Large.
    /// </summary>
    ResponseTooLarge,
}
