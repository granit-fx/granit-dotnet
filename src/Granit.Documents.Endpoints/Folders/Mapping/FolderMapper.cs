using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;

namespace Granit.Documents.Endpoints.Folders.Mapping;

/// <summary>Maps the <see cref="Folder"/> aggregate to wire-shape DTOs.</summary>
internal static class FolderMapper
{
    public static FolderResponse ToResponse(this Folder folder, EffectivePermissionLevel? permission = null) =>
        new(
            folder.Id,
            folder.ParentFolderId,
            folder.Name,
            folder.Path,
            folder.Depth,
            folder.OwnerUserId,
            folder.Status.ToString(),
            folder.TrashedAt,
            permission?.ToString());
}
