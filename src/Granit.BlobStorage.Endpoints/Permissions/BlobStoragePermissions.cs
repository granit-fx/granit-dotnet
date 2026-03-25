namespace Granit.BlobStorage.Endpoints.Permissions;

/// <summary>
/// Permission constants for blob storage administration.
/// </summary>
public static class BlobStoragePermissions
{
    public const string GroupName = "BlobStorage";

    /// <summary>Permissions for blob storage administration.</summary>
    public static class Administration
    {
        /// <summary>Grants read-only access to blob storage administration (list, descriptors, query).</summary>
        public const string Read = "BlobStorage.Administration.Read";

        /// <summary>Grants full management access to blob storage administration (upload, download, delete, confirm, cleanup).</summary>
        public const string Manage = "BlobStorage.Administration.Manage";
    }
}
