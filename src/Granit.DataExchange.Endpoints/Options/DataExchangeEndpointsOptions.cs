namespace Granit.DataExchange.Endpoints.Options;

/// <summary>
/// Configuration options for the data exchange endpoints (import + export).
/// Bind from <c>"DataExchangeEndpoints"</c> or pass an action to
/// <see cref="Extensions.DataExchangeEndpointRouteBuilderExtensions.MapDataExchangeEndpoints"/>.
/// </summary>
public sealed class DataExchangeEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DataExchangeEndpoints";

    /// <summary>
    /// Route prefix for all data exchange endpoints.
    /// Default: <c>"data-exchange"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "data-exchange";

    /// <summary>
    /// OpenAPI tag name for grouping data import endpoints in Swagger UI.
    /// Default: <c>"Data Exchange"</c>.
    /// </summary>
    public string TagName { get; set; } = "Data Exchange";
}
