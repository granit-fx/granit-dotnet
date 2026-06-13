using Granit.Identity.Endpoints.Endpoints;
using Granit.Identity.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the canonical user-session API (<c>/sessions</c> and <c>/devices</c>).
/// </summary>
public static class UserSessionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the canonical, self-service session and device endpoints. The whole surface requires an
    /// authenticated caller and operates on that caller's own sessions, regardless of the configured
    /// backend (BFF, OpenIddict or Keycloak).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="UserSessionsEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitUserSessions(
        this IEndpointRouteBuilder endpoints,
        Action<UserSessionsEndpointsOptions>? configure = null)
    {
        UserSessionsEndpointsOptions options = new();
        configure?.Invoke(options);

        string prefix = string.IsNullOrEmpty(options.RoutePrefix) ? "" : $"{options.RoutePrefix.Trim('/')}/";

        endpoints.MapGranitGroup($"{prefix}sessions")
            .WithTags(options.TagName)
            .RequireAuthorization()
            .MapMyUserSessionEndpoints();

        endpoints.MapGranitGroup($"{prefix}devices")
            .WithTags(options.TagName)
            .RequireAuthorization()
            .MapMyUserDeviceEndpoints();

        return endpoints;
    }
}
