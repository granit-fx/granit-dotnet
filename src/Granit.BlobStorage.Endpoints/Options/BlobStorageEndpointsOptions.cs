namespace Granit.BlobStorage.Endpoints.Options;

/// <summary>
/// Configuration options for the blob storage administration endpoints.
/// </summary>
public sealed class BlobStorageEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BlobStorageEndpoints";

    /// <summary>
    /// Route prefix for all blob storage endpoints.
    /// Default: <c>"blobs"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "blobs";

    /// <summary>
    /// Role required to access the administration endpoints.
    /// Default: <c>"granit-blobs-admin"</c>.
    /// </summary>
    public string RequiredRole { get; set; } = "granit-blobs-admin";

    /// <summary>
    /// OpenAPI tag name for grouping blob storage endpoints.
    /// Default: <c>"BlobStorage"</c>.
    /// </summary>
    public string TagName { get; set; } = "BlobStorage";
}
