namespace Granit.Mergeable.Exceptions;

/// <summary>
/// Thrown when a merge violates a hard invariant — tenant mismatch, kind mismatch, currency
/// mismatch, archived aggregate, already-merged loser, etc. Translates to HTTP 422 at the
/// endpoint boundary.
/// </summary>
public sealed class MergeException : InvalidOperationException
{
    /// <summary>Creates a new <see cref="MergeException"/>.</summary>
    public MergeException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="MergeException"/> with an inner exception.</summary>
    public MergeException(string message, Exception innerException) : base(message, innerException) { }
}
