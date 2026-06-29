namespace Granit.EntityMerge.Exceptions;

/// <summary>
/// Thrown when a merge violates a hard, unprocessable invariant — same survivor/loser id,
/// tenant mismatch, kind mismatch, currency mismatch, archived aggregate, etc. Translates to
/// HTTP 422 at the endpoint boundary.
/// </summary>
/// <remarks>
/// State conflicts and missing aggregates do NOT use this type: an already-merged survivor/loser
/// (and a reused idempotency key) throw <see cref="Granit.Exceptions.ConflictException"/> (409),
/// and a missing survivor/loser throws <see cref="Granit.Exceptions.EntityNotFoundException"/>
/// (404). Keeping these distinct lets the endpoint boundary map each to its correct status without
/// parsing the message.
/// </remarks>
public sealed class MergeException : InvalidOperationException
{
    /// <summary>Creates a new <see cref="MergeException"/>.</summary>
    public MergeException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="MergeException"/> with an inner exception.</summary>
    public MergeException(string message, Exception innerException) : base(message, innerException) { }
}
