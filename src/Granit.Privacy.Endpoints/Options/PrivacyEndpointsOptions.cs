namespace Granit.Privacy.Endpoints.Options;

/// <summary>
/// Configuration options for the privacy GDPR endpoints.
/// </summary>
public sealed class PrivacyEndpointsOptions
{
    /// <summary>
    /// Route prefix for all privacy endpoints.
    /// Default: <c>"privacy"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "privacy";

    /// <summary>
    /// OpenAPI tag name for grouping privacy endpoints.
    /// Default: <c>"Privacy"</c>.
    /// </summary>
    public string TagName { get; set; } = "Privacy";
}
