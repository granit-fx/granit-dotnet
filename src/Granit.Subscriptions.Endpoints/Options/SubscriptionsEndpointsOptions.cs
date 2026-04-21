namespace Granit.Subscriptions.Endpoints.Options;

/// <summary>
/// Configuration options for the subscriptions endpoints.
/// </summary>
public sealed class SubscriptionsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "SubscriptionsEndpoints";

    /// <summary>
    /// Route prefix for all subscription endpoints.
    /// Default: <c>"subscriptions"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "subscriptions";

    /// <summary>
    /// OpenAPI tag name for plan catalog endpoints.
    /// Default: <c>"Subscriptions - Plans"</c>.
    /// </summary>
    public string PlansTagName { get; set; } = "Subscriptions - Plans";

    /// <summary>
    /// OpenAPI tag name for price version endpoints.
    /// Default: <c>"Subscriptions - Prices"</c>.
    /// </summary>
    public string PricesTagName { get; set; } = "Subscriptions - Prices";

    /// <summary>
    /// OpenAPI tag name for subscription lifecycle endpoints.
    /// Default: <c>"Subscriptions - Subscriptions"</c>.
    /// </summary>
    public string SubscriptionsTagName { get; set; } = "Subscriptions - Subscriptions";

    /// <summary>
    /// OpenAPI tag name for seat assignment endpoints.
    /// Default: <c>"Subscriptions - Seats"</c>.
    /// </summary>
    public string SeatsTagName { get; set; } = "Subscriptions - Seats";
}
