namespace Granit.Authorization.Endpoints.Options;

/// <summary>
/// Configuration options for the authorization management endpoints.
/// Bind from <c>"AuthorizationEndpoints"</c> or pass an action to
/// <see cref="Extensions.AuthorizationEndpointRouteBuilderExtensions.MapGranitAuthorization"/>.
/// </summary>
public sealed class AuthorizationEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AuthorizationEndpoints";

    /// <summary>
    /// Route prefix for all authorization endpoints.
    /// Default: <c>"auth"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "auth";

    /// <summary>
    /// OpenAPI tag name for grouping authorization endpoints in Swagger UI.
    /// Default: <c>"Authorization"</c>.
    /// </summary>
    public string TagName { get; set; } = "Authorization";
}
