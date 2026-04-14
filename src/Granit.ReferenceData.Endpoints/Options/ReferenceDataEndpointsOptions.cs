namespace Granit.ReferenceData.Endpoints.Options;

/// <summary>
/// Configuration options for reference data endpoints.
/// </summary>
public sealed class ReferenceDataEndpointsOptions
{
    /// <summary>
    /// Route prefix for all reference data endpoints.
    /// Default: <c>"reference-data"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "reference-data";

    /// <summary>
    /// OpenAPI tag name for grouping reference data endpoints in Scalar UI.
    /// Default: <c>"Reference Data"</c>.
    /// </summary>
    public string TagName { get; set; } = "Reference Data";

    /// <summary>
    /// Explicit override for the route segment derived from the entity type name.
    /// When set, bypasses the automatic pluralization and kebab-case conversion.
    /// Default: <c>null</c> (auto-generated from entity type name).
    /// </summary>
    /// <example>
    /// Setting <c>EntitySegment = "people"</c> for a <c>Person</c> entity
    /// produces <c>/reference-data/people</c> instead of <c>/reference-data/persons</c>.
    /// </example>
    public string? EntitySegment { get; set; }

    /// <summary>
    /// Whether to register a <c>GET /meta</c> endpoint returning query metadata
    /// (columns, filters, sorts, presets) for dynamic UI construction.
    /// Default: <c>true</c>.
    /// </summary>
    public bool IncludeMetaEndpoint { get; set; } = true;
}
