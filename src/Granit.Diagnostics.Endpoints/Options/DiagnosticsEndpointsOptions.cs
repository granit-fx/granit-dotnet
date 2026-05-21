namespace Granit.Diagnostics.Endpoints.Options;

/// <summary>
/// Options for diagnostics monitoring endpoints.
/// </summary>
public sealed class DiagnosticsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Diagnostics:Endpoints";

    /// <summary>
    /// Route prefix for monitoring endpoints.
    /// Default: <c>"diagnostics"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "diagnostics";

    /// <summary>
    /// OpenAPI tag name for monitoring endpoints.
    /// Default: <c>"Diagnostics"</c>.
    /// </summary>
    public string TagName { get; set; } = "Diagnostics";
}
