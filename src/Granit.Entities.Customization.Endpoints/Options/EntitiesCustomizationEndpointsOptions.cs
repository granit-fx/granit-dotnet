namespace Granit.Entities.Customization.Endpoints.Options;

/// <summary>
/// Configuration options for the entities-customization endpoints.
/// </summary>
public sealed class EntitiesCustomizationEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "EntitiesCustomizationEndpoints";

    /// <summary>Route prefix for all customization endpoints. Default: <c>"entities"</c>.</summary>
    public string RoutePrefix { get; set; } = "entities";

    /// <summary>OpenAPI tag name for grouping endpoints in Scalar / Swagger UI. Default: <c>"Customization"</c>.</summary>
    public string TagName { get; set; } = "Customization";

    /// <summary>
    /// Maximum number of deltas accepted in a single PUT. Default: <c>100</c>.
    /// Tenants that hit this cap are typically in the wrong layer and should be
    /// reaching for Layer 2 custom fields (Phase 4) instead.
    /// </summary>
    public int MaxDeltasPerRequest { get; set; } = 100;
}
