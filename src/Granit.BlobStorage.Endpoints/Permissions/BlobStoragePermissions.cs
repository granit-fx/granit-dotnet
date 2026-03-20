namespace Granit.BlobStorage.Endpoints.Permissions;

/// <summary>
/// Permission constants for blob storage administration.
/// </summary>
public static class BlobStoragePermissions
{
    public const string GroupName = "BlobStorage";

    public static class Blobs
    {
        public const string Read = "BlobStorage.Blobs.Read";
        public const string Upload = "BlobStorage.Blobs.Upload";
        public const string Download = "BlobStorage.Blobs.Download";
        public const string Delete = "BlobStorage.Blobs.Delete";
        public const string Manage = "BlobStorage.Blobs.Manage";
    }
}
