namespace Granit.Geocoding.Endpoints.Options;

/// <summary>
/// Options for the geocoding HTTP endpoints.
/// </summary>
public sealed class GeocodingEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Geocoding:Endpoints";

    /// <summary>Route prefix for the geocoding endpoints. Default: <c>"geocoding"</c>.</summary>
    public string RoutePrefix { get; set; } = "geocoding";

    /// <summary>OpenAPI tag for the geocoding endpoints. Default: <c>"Geocoding"</c>.</summary>
    public string TagName { get; set; } = "Geocoding";

    /// <summary>
    /// Authorization policy applied to the group. When <see langword="null"/> or empty (the default), the
    /// endpoints require <strong>any authenticated user</strong> — geocoding proxies an external, rate-limited,
    /// billable provider, so it is never mapped anonymous. Set a policy name to require a specific policy.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }
}
