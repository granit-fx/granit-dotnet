using Granit.Http.ODataExposure.Diagnostics;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0058 // Expression value is never used — fluent ODataMiniOptions config returns the options instance

namespace Granit.Http.ODataExposure.Extensions;

/// <summary>
/// DI registration helpers for <c>Granit.Http.ODataExposure</c>.
/// </summary>
public static class ODataExposureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OData runtime infrastructure required by
    /// <see cref="ODataExposureEndpointRouteBuilderExtensions.MapGranitODataEndpoints"/>.
    /// Enables the standard OData query features ($filter, $select, $top,
    /// $skip, $orderby, $expand) so they are available on every exposed
    /// EntitySet — per-set restrictions (e.g. expand whitelist, max top) are
    /// applied per route in a follow-up story (#1392 / C3).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitODataExposure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AddOData wires the OData formatter, the ODataQueryOptions<T>
        // parameter binder, and the routing helpers used by WithODataResult
        // / WithODataModel. The .Mini variant lives directly on
        // IServiceCollection (no MVC stack required) — this matters for
        // minimal-API-only hosts. EnableAll() turns on every standard query
        // option ($filter / $select / $top / $skip / $orderby / $expand /
        // $count); per-route restrictions are layered via
        // AddODataQueryEndpointFilter in #1392 / C3.
        services.AddOData(opt => opt.EnableAll());

        // C3 hardening telemetry: counts rejected queries (count disabled,
        // expand not whitelisted) and top-clamps. Always on so observability
        // tooling can spot misconfigured BI refresh jobs without per-host
        // setup.
        services.TryAddSingleton<ODataExposureMetrics>();

        return services;
    }
}
