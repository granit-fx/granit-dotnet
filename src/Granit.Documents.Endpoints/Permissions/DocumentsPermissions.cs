namespace Granit.Documents.Endpoints.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Documents.Endpoints</c>.
/// </summary>
/// <remarks>
/// Phase 1 ships only the folder permissions used by F2.3. Document, share, tag, and
/// quota permissions arrive with their respective stories (F3, F6, F5, F7).
/// </remarks>
public static class DocumentsPermissions
{
    /// <summary>Permission group name (used as the resource-key prefix for localisation).</summary>
    public const string GroupName = "Documents";

    /// <summary>Permissions on the folder hierarchy.</summary>
    public static class Folders
    {
        /// <summary>Read folders (list, get, breadcrumb).</summary>
        public const string Read = "Documents.Folders.Read";

        /// <summary>Manage folders (create, rename, move, trash, restore, permanent delete).</summary>
        public const string Manage = "Documents.Folders.Manage";
    }
}
