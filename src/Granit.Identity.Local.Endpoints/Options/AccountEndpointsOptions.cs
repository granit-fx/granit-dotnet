namespace Granit.Identity.Local.Endpoints.Options;

/// <summary>
/// Options for configuring account self-service endpoints.
/// </summary>
public sealed class AccountEndpointsOptions
{
    /// <summary>Route prefix for account self-service endpoints. Default: <c>"api/account"</c>.</summary>
    public string AccountRoutePrefix { get; set; } = "api/account";

    /// <summary>Route prefix for admin management endpoints. Default: <c>"api/admin"</c>.</summary>
    public string AdminRoutePrefix { get; set; } = "api/admin";
}
