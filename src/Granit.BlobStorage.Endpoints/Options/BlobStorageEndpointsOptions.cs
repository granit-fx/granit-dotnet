namespace Granit.BlobStorage.Endpoints.Options;

/// <summary>
/// Configuration options for the blob storage administration endpoints.
/// </summary>
public sealed class BlobStorageEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BlobStorage:Endpoints";

    /// <summary>
    /// Route prefix for all blob storage endpoints.
    /// Default: <c>"blob-storage"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "blob-storage";

    /// <summary>
    /// OpenAPI tag name for grouping blob storage endpoints.
    /// Default: <c>"Blob Storage"</c>.
    /// </summary>
    public string TagName { get; set; } = "Blob Storage";
}
