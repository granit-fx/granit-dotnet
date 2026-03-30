namespace Granit.Privacy.OptOut;

/// <summary>
/// Reads opt-out records. Supports both authenticated users and anonymous visitors.
/// </summary>
public interface IOptOutRecordReader
{
    /// <summary>Returns the opt-out record for the specified authenticated user, or <c>null</c>.</summary>
    Task<OptOutRecord?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the opt-out record for the specified anonymous tracking ID, or <c>null</c>.</summary>
    Task<OptOutRecord?> GetByAnonymousTrackAsync(string trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if the user or anonymous visitor has an active opt-out.
    /// Checks <paramref name="userId"/> first, then <paramref name="anonymousTrackId"/>.
    /// </summary>
    Task<bool> IsOptedOutAsync(Guid? userId, string? anonymousTrackId, CancellationToken cancellationToken = default);
}
