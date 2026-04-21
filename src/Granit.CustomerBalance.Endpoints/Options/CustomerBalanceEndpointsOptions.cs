namespace Granit.CustomerBalance.Endpoints.Options;

/// <summary>
/// Configuration options for the customer balance endpoints.
/// </summary>
public sealed class CustomerBalanceEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "CustomerBalanceEndpoints";

    /// <summary>
    /// Route prefix for all customer balance endpoints.
    /// Default: <c>"customer-balance"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "customer-balance";

    /// <summary>
    /// OpenAPI tag name for customer balance endpoints.
    /// Default: <c>"Customer Balance"</c>.
    /// </summary>
    public string TagName { get; set; } = "Customer Balance";
}
