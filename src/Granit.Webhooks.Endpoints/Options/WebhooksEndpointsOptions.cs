namespace Granit.Webhooks.Endpoints.Options;

/// <summary>
/// Configuration options for the webhook administration endpoints.
/// </summary>
public sealed class WebhooksEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "WebhooksEndpoints";

    /// <summary>
    /// Route prefix for all webhook endpoints.
    /// Default: <c>"webhooks"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "webhooks";

    /// <summary>
    /// OpenAPI tag name for grouping webhook endpoints.
    /// Default: <c>"Webhooks"</c>.
    /// </summary>
    public string TagName { get; set; } = "Webhooks";
}
