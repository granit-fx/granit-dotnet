namespace Granit.Privacy.BlobStorage.Endpoints.Options;

/// <summary>
/// Configuration options for the blob-backed privacy export download endpoint.
/// Mirrors the defaults of
/// <see cref="Extensions.PrivacyBlobStorageEndpointRouteBuilderExtensions.MapGranitPrivacyExportDownload"/>,
/// which also accepts both values as method parameters for hosts that wire routes explicitly.
/// </summary>
public sealed class PrivacyBlobStorageEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Privacy:BlobStorage:Endpoints";

    /// <summary>
    /// Route prefix for the download endpoint — typically mirrors <c>MapGranitPrivacy</c>.
    /// Default: <c>"privacy"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "privacy";

    /// <summary>
    /// OpenAPI tag name for the download endpoint. Default: <c>"Privacy"</c> — matches
    /// <c>PrivacyEndpointsOptions.TagName</c> so the route groups with the other privacy
    /// endpoints in generated clients.
    /// </summary>
    public string TagName { get; set; } = "Privacy";
}
