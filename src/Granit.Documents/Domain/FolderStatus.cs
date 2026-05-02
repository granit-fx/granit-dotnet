namespace Granit.Documents.Domain;

/// <summary>
/// Lifecycle status of a <see cref="Folder"/>.
/// </summary>
/// <remarks>
/// Trashed folders are retained for the duration of <c>DocumentsOptions.TrashRetentionDays</c>
/// before permanent deletion by the empty-trash background job (F8 / F9.2).
/// </remarks>
public enum FolderStatus
{
    /// <summary>The folder is active and visible in browsing / search.</summary>
    Active,

    /// <summary>The folder has been moved to the trash; awaiting restore or permanent deletion.</summary>
    Trashed,
}
