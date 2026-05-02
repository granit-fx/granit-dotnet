namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape response for <c>GET /folders</c>: a flat list of active children under the
/// requested parent (the tenant root by default). The tenant root itself is filtered out.
/// </summary>
/// <param name="Folders">Children of the parent, ordered alphabetically by name.</param>
public sealed record ListFoldersResponse(IReadOnlyList<FolderResponse> Folders);
