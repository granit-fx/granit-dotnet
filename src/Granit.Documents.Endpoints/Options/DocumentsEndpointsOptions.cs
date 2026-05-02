namespace Granit.Documents.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Documents HTTP endpoints.
/// </summary>
public sealed class DocumentsEndpointsOptions
{
    /// <summary>Section key in the configuration (<c>"DocumentsEndpoints"</c>).</summary>
    public const string SectionName = "DocumentsEndpoints";

    /// <summary>
    /// Route prefix for all Documents endpoints. Default: <c>"documents"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "documents";

    /// <summary>
    /// OpenAPI tag name applied to the root <c>RouteGroupBuilder</c>. Default: <c>"Documents"</c>.
    /// </summary>
    public string TagName { get; set; } = "Documents";

    /// <summary>
    /// Rate-limiting policy name applied to all Documents endpoints. When <c>null</c>
    /// (default), no rate limiting is applied. Set to a policy name registered via
    /// <c>AddRateLimiter()</c> to protect against resource exhaustion via excessive
    /// folder list / breadcrumb traversal calls.
    /// </summary>
    public string? RateLimitingPolicy { get; set; }
}
