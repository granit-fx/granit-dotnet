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

    /// <summary>Permissions on documents.</summary>
    public static class Documents
    {
        /// <summary>Read documents (download, get metadata, list versions).</summary>
        public const string Read = "Documents.Documents.Read";

        /// <summary>Manage documents (upload, rename, move, trash, restore, permanent delete).</summary>
        public const string Manage = "Documents.Documents.Manage";
    }

    /// <summary>Permissions on share ACL grants (F6).</summary>
    public static class Shares
    {
        /// <summary>List share grants on a folder or a document.</summary>
        public const string Read = "Documents.Shares.Read";

        /// <summary>Grant and revoke share ACL on folders and documents.</summary>
        public const string Manage = "Documents.Shares.Manage";
    }
}
