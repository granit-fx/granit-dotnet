namespace Granit.Privacy.DataExport;

/// <summary>
/// Read-only abstraction for querying GDPR export request status.
/// Implemented by the application's persistence layer (EF Core, etc.).
/// </summary>
/// <remarks>
/// The Wolverine export saga state is internal; this interface provides the read model
/// that endpoints use to report export progress to the frontend.
/// </remarks>
public interface IExportRequestTrackerReader
{
    /// <summary>Returns the status of a single export request, or <c>null</c> if not found.</summary>
    Task<ExportRequestStatus?> GetStatusAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Returns all export requests for a given user, ordered by most recent first.</summary>
    Task<IReadOnlyList<ExportRequestStatus>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
