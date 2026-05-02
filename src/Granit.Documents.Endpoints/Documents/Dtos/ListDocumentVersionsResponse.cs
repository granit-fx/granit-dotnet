namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape response for <c>GET /documents/{id}/versions</c>: a paged slice of the
/// version history along with the total count so the frontend can render pagination
/// controls.
/// </summary>
/// <param name="Versions">Page slice ordered by version number descending (latest first).</param>
/// <param name="TotalCount">Total number of versions across all pages.</param>
/// <param name="Skip">Number of versions skipped before this slice (echoes the request).</param>
/// <param name="Take">Maximum number of versions returned (echoes the request).</param>
public sealed record ListDocumentVersionsResponse(
    IReadOnlyList<DocumentVersionResponse> Versions,
    int TotalCount,
    int Skip,
    int Take);
