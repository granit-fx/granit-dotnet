namespace Granit.Validation.Endpoints.Options;

/// <summary>
/// Options for server-side validation endpoints.
/// </summary>
public sealed class ValidationEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Validation:Endpoints";

    /// <summary>
    /// Route prefix for all validation endpoints.
    /// Default: <c>"validation"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "validation";

    /// <summary>
    /// OpenAPI tag name for all validation endpoints.
    /// Default: <c>"Validation"</c>.
    /// </summary>
    public string TagName { get; set; } = "Validation";
}
