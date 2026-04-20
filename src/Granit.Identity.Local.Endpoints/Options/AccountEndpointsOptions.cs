namespace Granit.Identity.Local.Endpoints.Options;

/// <summary>
/// Options for configuring account self-service endpoints.
/// </summary>
public sealed class AccountEndpointsOptions
{
    /// <summary>Route prefix for account self-service endpoints. Default: <c>"account"</c>.</summary>
    public string AccountRoutePrefix { get; set; } = "account";

    /// <summary>Route prefix for admin management endpoints. Default: <c>"admin"</c>.</summary>
    public string AdminRoutePrefix { get; set; } = "admin";

    /// <summary>OpenAPI tag name for account self-service endpoints. Default: <c>"Account"</c>.</summary>
    public string AccountTagName { get; set; } = "Account";

    /// <summary>OpenAPI tag name for admin impersonation endpoints. Default: <c>"Admin Impersonation"</c>.</summary>
    public string AdminTagName { get; set; } = "Admin Impersonation";
}
