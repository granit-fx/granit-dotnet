using Granit.Localization.Domain;
using Granit.Localization.Endpoints.Endpoints;
using Granit.Localization.Endpoints.Options;
using Granit.Localization.Endpoints.Permissions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Localization.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit localization endpoints.
/// </summary>
public static class LocalizationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET /{prefix}/localization</c> — returns all registered localization
    /// resources for the requested culture, plus the list of available languages.
    /// </summary>
    /// <remarks>
    /// <para>The endpoint is anonymous: translation strings are public UI data.</para>
    /// <para>
    /// Response headers include <c>Cache-Control: public, max-age=3600</c> and
    /// <c>Vary: Accept-Language</c> to allow browser and CDN caching per culture.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="LocalizationEndpointsOptions"/>.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitLocalization(
        this IEndpointRouteBuilder endpoints,
        Action<LocalizationEndpointsOptions>? configure = null)
    {
        LocalizationEndpointsOptions options = new();
        configure?.Invoke(options);

        endpoints.MapLocalizationReadEndpoints(options.RoutePrefix, options.TagName);

        return endpoints;
    }

    /// <summary>
    /// Maps the localization override management endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers query-engine endpoints + CRUD under <c>/{prefix}/localization/overrides</c>:
    /// <list type="bullet">
    /// <item><c>GET /</c> — paginated, filterable, sortable list of overrides (query engine)</item>
    /// <item><c>GET /meta</c> — query metadata (columns, filters, sorts, presets)</item>
    /// <item><c>PUT /{resourceName}/{cultureName}/{key}</c> — create or update an override</item>
    /// <item><c>DELETE /{resourceName}/{cultureName}/{key}</c> — remove an override</item>
    /// </list>
    /// </para>
    /// <para>
    /// The list/meta endpoints require an <c>IQueryableSource&lt;LocalizationOverride&gt;</c>
    /// to be registered (provided by <c>AddGranitLocalizationEntityFrameworkCore</c>).
    /// The PUT/DELETE endpoints require <c>ILocalizationOverrideStoreWriter</c> to be registered
    /// (same call); without it they return <c>501 Not Implemented</c>.
    /// </para>
    /// <para>
    /// All endpoints require the <c>Localization.Overrides.Manage</c> permission.
    /// </para>
    /// <para>
    /// <b>Breaking change vs. earlier versions:</b> the legacy
    /// <c>GET /overrides?resourceName=X&amp;cultureName=Y</c> shape has been replaced by the
    /// query-engine surface (<c>GET /</c> + <c>GET /meta</c>). Frontends that previously
    /// called the legacy shape must switch to the query-engine contract (e.g.
    /// <c>useQueryEndpoint</c> / <c>useQueryMeta</c>).
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="LocalizationEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitLocalizationOverrides(
        this IEndpointRouteBuilder endpoints,
        Action<LocalizationEndpointsOptions>? configure = null)
    {
        LocalizationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup($"{options.RoutePrefix}/overrides")
            .RequireAuthorization(LocalizationOverridesPermissions.Overrides.Manage)
            .WithTags(options.TagName);

        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. To expose cross-tenant localization-override visibility, mark
        // this route .AllowHostAccess(); a platform admin holding this group's read permission at
        // global scope then reads across tenants, while the filter stays enforced for tenant callers.
        group.MapGranitQuery<LocalizationOverride>();
        group.MapLocalizationWriteEndpoints();

        return group;
    }
}
