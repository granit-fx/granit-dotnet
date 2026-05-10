namespace Granit.Documents.Endpoints.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Documents.Endpoints</c> — folder, document,
/// share, tag, and quota permissions used by the Phase 1 Granit.Documents endpoints.
/// </summary>
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

    /// <summary>Permissions on the document tag proxy (F5 / T6.1).</summary>
    public static class Tags
    {
        /// <summary>List tags assigned to a document.</summary>
        public const string Read = "Documents.Tags.Read";

        /// <summary>Assign or unassign tags on a document.</summary>
        public const string Manage = "Documents.Tags.Manage";
    }

    /// <summary>Permissions on the tenant storage quota (F7).</summary>
    public static class Quotas
    {
        /// <summary>Read the tenant's storage quota usage.</summary>
        public const string Read = "Documents.Quotas.Read";
    }
}
