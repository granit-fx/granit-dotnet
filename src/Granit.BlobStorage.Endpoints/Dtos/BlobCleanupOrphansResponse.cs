namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Response after cleaning up orphaned blobs.
/// </summary>
public sealed record BlobCleanupOrphansResponse(int CleanedCount);
