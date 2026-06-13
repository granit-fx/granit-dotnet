using Granit.Identity.Endpoints.Endpoints;
using Granit.Identity.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping the canonical user-session API (<c>/sessions</c> and <c>/devices</c>).
/// </summary>
public static partial class UserSessionEndpointRouteBuilderExtensions
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

        WarnIfNoSessionBackend(endpoints);

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

    // The canonical API resolves everywhere thanks to the no-op defaults, so a host that maps it without
    // wiring a backend gets a clean 200 [] with no error — easy to mistake for "no sessions". Surface it
    // loudly at startup instead. Resolving the provider here (the container is built by map time) costs one
    // scoped instance; in the failure case it's the cheap no-op.
    private static void WarnIfNoSessionBackend(IEndpointRouteBuilder endpoints)
    {
        using IServiceScope scope = endpoints.ServiceProvider.CreateScope();
        IUserSessionProvider? provider = scope.ServiceProvider.GetService<IUserSessionProvider>();
        // Null (nothing registered) or the no-op default both mean the endpoints will serve empty data.
        if (provider is not (null or IFallbackUserSessionProvider))
        {
            return;
        }

        LogNoSessionBackend(endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.UserSessions"));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The canonical user-session API (/sessions, /devices) is mapped but no backend session " +
                  "provider is registered — both endpoints will return empty. Wire a backend: call its " +
                  "AddGranit* extension (e.g. AddGranitOpenIddict, AddGranitIdentityKeycloak) or add its " +
                  "module (e.g. GranitBffUserSessionsModule) to your AddGranit graph.")]
    private static partial void LogNoSessionBackend(ILogger logger);
}
