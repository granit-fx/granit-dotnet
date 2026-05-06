namespace Granit.Taxonomy.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Taxonomy HTTP endpoints.
/// </summary>
public sealed class TaxonomyEndpointsOptions
{
    /// <summary>Section key in the configuration (<c>"TaxonomyEndpoints"</c>).</summary>
    public const string SectionName = "TaxonomyEndpoints";

    /// <summary>Route prefix for all Taxonomy endpoints. Default: <c>"taxonomy"</c>.</summary>
    public string RoutePrefix { get; set; } = "taxonomy";

    /// <summary>OpenAPI tag name applied to the root <c>RouteGroupBuilder</c>. Default: <c>"Taxonomy"</c>.</summary>
    public string TagName { get; set; } = "Taxonomy";

    /// <summary>
    /// Rate-limiting policy name applied to all Taxonomy endpoints. When <c>null</c>
    /// (default), no rate limiting is applied.
    /// </summary>
    public string? RateLimitingPolicy { get; set; }
}
