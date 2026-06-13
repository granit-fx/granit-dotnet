namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration for the canonical user-session endpoints. Bind from
/// <c>"Identity:Endpoints:Sessions"</c> or pass an action to
/// <see cref="Extensions.UserSessionEndpointRouteBuilderExtensions.MapGranitUserSessions"/>.
/// </summary>
public sealed class UserSessionsEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:Endpoints:Sessions";

    /// <summary>
    /// Optional route prefix prepended to <c>sessions</c> and <c>devices</c> (e.g. <c>"account"</c> →
    /// <c>/account/sessions</c>). Default: empty (routes are mapped at the root).
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>OpenAPI tag grouping the session and device endpoints. Default: <c>"Identity - User Sessions"</c>.</summary>
    public string TagName { get; set; } = "Identity - User Sessions";
}
