namespace Granit.BlobStorage.Proxy.Options;

/// <summary>
/// Configuration for the blob storage proxy endpoints.
/// </summary>
/// <remarks>
/// Bind from the <c>BlobStorage:Proxy</c> configuration section.
/// <see cref="BaseUrl"/> must point to the externally reachable application URL
/// so that generated proxy URLs are valid for clients.
/// </remarks>
public sealed class ProxyBlobOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BlobStorage:Proxy";

    /// <summary>
    /// External base URL used to build proxy upload/download URLs.
    /// Must be an absolute URI (e.g. <c>https://api.example.com</c>).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Route prefix for proxy endpoints. Defaults to <c>/api/blobs</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "/api/blobs";

    /// <summary>
    /// Maximum upload size in bytes accepted by the proxy. Defaults to 100 MB.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 104_857_600;

    /// <summary>
    /// OpenAPI tag name for proxy endpoints.
    /// Default: <c>"Blob Proxy"</c>.
    /// </summary>
    public string TagName { get; set; } = "Blob Proxy";
}
