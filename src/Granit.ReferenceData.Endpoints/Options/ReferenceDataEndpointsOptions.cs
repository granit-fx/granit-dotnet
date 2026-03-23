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
}
