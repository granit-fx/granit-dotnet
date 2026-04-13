namespace Granit.OpenIddict.Endpoints.Options;

/// <summary>
/// Options for configuring OpenIddict account endpoints.
/// </summary>
public sealed class OpenIddictEndpointsOptions
{
    /// <summary>Route prefix for account self-service endpoints. Default: <c>"account"</c>.</summary>
    public string AccountRoutePrefix { get; set; } = "account";

    /// <summary>Route prefix for admin management endpoints. Default: <c>"admin"</c>.</summary>
    public string AdminRoutePrefix { get; set; } = "admin";
}
