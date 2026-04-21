namespace Granit.Invoicing.Endpoints.Options;

/// <summary>
/// Configuration options for the invoicing endpoints.
/// </summary>
public sealed class InvoicingEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "InvoicingEndpoints";

    /// <summary>
    /// Route prefix for all invoicing endpoints.
    /// Default: <c>"invoicing"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "invoicing";

    /// <summary>
    /// OpenAPI tag name for invoicing endpoints.
    /// Default: <c>"Invoicing"</c>.
    /// </summary>
    public string TagName { get; set; } = "Invoicing";
}
