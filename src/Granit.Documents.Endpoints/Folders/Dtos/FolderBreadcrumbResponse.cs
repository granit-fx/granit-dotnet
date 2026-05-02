namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape response for <c>GET /folders/{id}/breadcrumb</c>: the chain of folders
/// from the first user-visible folder under the tenant root down to and including the
/// requested folder.
/// </summary>
/// <param name="Folders">Ordered list (closest-to-root first; the requested folder is the last entry).</param>
public sealed record FolderBreadcrumbResponse(IReadOnlyList<FolderResponse> Folders);
