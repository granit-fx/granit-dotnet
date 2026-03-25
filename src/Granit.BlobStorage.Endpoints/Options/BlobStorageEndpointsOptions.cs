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
    /// OpenAPI tag name for grouping blob storage endpoints.
    /// Default: <c>"BlobStorage"</c>.
    /// </summary>
    public string TagName { get; set; } = "BlobStorage";
}
