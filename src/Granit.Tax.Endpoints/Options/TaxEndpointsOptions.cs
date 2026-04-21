namespace Granit.Tax.Endpoints.Options;

/// <summary>
/// Configuration options for the tax endpoints.
/// </summary>
public sealed class TaxEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TaxEndpoints";

    /// <summary>
    /// Route prefix for all tax endpoints.
    /// Default: <c>"tax"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "tax";

    /// <summary>
    /// OpenAPI tag name for tax-ID validation endpoints.
    /// Default: <c>"Tax - Validation"</c>.
    /// </summary>
    public string ValidationTagName { get; set; } = "Tax - Validation";

    /// <summary>
    /// OpenAPI tag name for tax rate lookup endpoints.
    /// Default: <c>"Tax - Rates"</c>.
    /// </summary>
    public string RatesTagName { get; set; } = "Tax - Rates";
}
