namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration options for identity provider administration endpoints.
/// </summary>
public sealed class IdentityProviderEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "IdentityProviderEndpoints";

    /// <summary>
    /// Route prefix for all identity provider endpoints.
    /// Default: <c>"identity/provider"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "identity/provider";

    /// <summary>
    /// OpenAPI tag name for grouping identity provider endpoints.
    /// Default: <c>"Identity Provider"</c>.
    /// </summary>
    public string TagName { get; set; } = "Identity Provider";
}
