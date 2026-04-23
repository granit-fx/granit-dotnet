namespace Granit.DataLookup.Endpoints.Options;

/// <summary>
/// Options controlling the routing and OpenAPI presentation of the data-lookup endpoints.
/// </summary>
public sealed class DataLookupEndpointsOptions
{
    /// <summary>Route prefix under which the endpoints are mapped. Default: <c>"api/granit/lookups"</c>.</summary>
    public string RoutePrefix { get; set; } = "api/granit/lookups";

    /// <summary>OpenAPI tag applied to every endpoint in the group. Default: <c>"Data Lookup"</c>.</summary>
    public string TagName { get; set; } = "Data Lookup";
}
