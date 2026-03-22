namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Reads the status of deferred deletion requests (CQRS read side).
/// Application must provide a concrete implementation via
/// <see cref="GranitPrivacyBuilder.UseDeletionRequestTracker{TStore}"/>.
/// </summary>
public interface IDeletionRequestTrackerReader
{
    /// <summary>Gets the status of a specific deletion request.</summary>
    Task<DeletionRequestStatus?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Gets all deletion requests for a user (most recent first).</summary>
    Task<IReadOnlyList<DeletionRequestStatus>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets all deferred requests whose scheduled deletion date has passed.</summary>
    Task<IReadOnlyList<DeletionRequestStatus>> GetExpiredDeferredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
