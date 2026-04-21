namespace Granit.Payments.Endpoints.Options;

/// <summary>
/// Configuration options for the payments endpoints.
/// </summary>
public sealed class PaymentsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PaymentsEndpoints";

    /// <summary>
    /// Route prefix for all payment endpoints.
    /// Default: <c>"payments"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "payments";

    /// <summary>
    /// OpenAPI tag name for transaction endpoints.
    /// Default: <c>"Payments - Transactions"</c>.
    /// </summary>
    public string TransactionsTagName { get; set; } = "Payments - Transactions";

    /// <summary>
    /// OpenAPI tag name for saved payment method endpoints.
    /// Default: <c>"Payments - Methods"</c>.
    /// </summary>
    public string MethodsTagName { get; set; } = "Payments - Methods";

    /// <summary>
    /// OpenAPI tag name for payment method configuration (activation) endpoints.
    /// Default: <c>"Payments - Configuration"</c>.
    /// </summary>
    public string ConfigurationTagName { get; set; } = "Payments - Configuration";

    /// <summary>
    /// OpenAPI tag name for inbound provider webhook endpoints.
    /// Default: <c>"Payments - Webhooks"</c>.
    /// </summary>
    public string WebhooksTagName { get; set; } = "Payments - Webhooks";
}
