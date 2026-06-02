using Granit.Settings.Endpoints.Endpoints;
using Granit.Settings.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Settings.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping user-scoped setting endpoints.
/// </summary>
public static class UserSettingsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps user-scoped setting endpoints under <c>/{prefix}/settings/user</c>.
    /// All endpoints require authentication.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitUserSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.UserRoutePrefix)
            .RequireAuthorization()
            .WithTags(options.UserTagName);

        group.MapUserSettingsReadEndpoints();
        group.MapUserSettingsWriteEndpoints();

        return group;
    }
}
