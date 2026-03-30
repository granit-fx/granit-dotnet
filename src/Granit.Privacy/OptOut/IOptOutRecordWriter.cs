namespace Granit.Privacy.OptOut;

/// <summary>
/// Writes opt-out records. Supports both authenticated users and anonymous visitors.
/// </summary>
public interface IOptOutRecordWriter
{
    /// <summary>Records a new opt-out request.</summary>
    Task RecordOptOutAsync(OptOutRecord record, CancellationToken cancellationToken = default);

    /// <summary>Revokes an existing opt-out.</summary>
    Task RevokeOptOutAsync(Guid recordId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers an anonymous opt-out to an authenticated user profile.
    /// Called when a user logs in after opting out anonymously (Cart Merge pattern).
    /// </summary>
    Task MergeAnonymousAsync(string anonymousTrackId, Guid userId, CancellationToken cancellationToken = default);
}
