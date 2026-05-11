namespace Granit.Documents.PublicLinks.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Documents.PublicLinks HTTP endpoints.
/// </summary>
public sealed class DocumentsPublicLinksEndpointsOptions
{
    /// <summary>Section key in the configuration (<c>"DocumentsPublicLinksEndpoints"</c>).</summary>
    public const string SectionName = "DocumentsPublicLinksEndpoints";

    /// <summary>
    /// Route prefix for the authenticated admin endpoints (list / create / revoke).
    /// Default: <c>"documents"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "documents";

    /// <summary>
    /// Route prefix for the anonymous redemption endpoints. Kept short on purpose —
    /// the bearer token sits in the path and short prefixes keep the shareable URL
    /// compact. Default: <c>"p"</c>.
    /// </summary>
    public string AnonymousRoutePrefix { get; set; } = "p";

    /// <summary>
    /// OpenAPI tag applied to every endpoint in this module.
    /// Default: <c>"Documents - Public Links"</c>.
    /// </summary>
    public string TagName { get; set; } = "Documents - Public Links";
}
