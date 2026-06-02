using Granit.Settings.Endpoints.Endpoints;
using Granit.Settings.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Settings.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping global and tenant setting administration endpoints.
/// </summary>
public static class AdminSettingsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps global setting administration endpoints under <c>/{prefix}/settings/global</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitGlobalSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.GlobalRoutePrefix)
            .WithTags(options.GlobalTagName);

        group.MapGlobalSettingsReadEndpoints();
        group.MapGlobalSettingsWriteEndpoints();

        return group;
    }

    /// <summary>
    /// Maps tenant-scoped setting administration endpoints under <c>/{prefix}/settings/tenant</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTenantSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.TenantRoutePrefix)
            .WithTags(options.TenantTagName);

        group.MapTenantSettingsReadEndpoints();
        group.MapTenantSettingsWriteEndpoints();

        return group;
    }
}
