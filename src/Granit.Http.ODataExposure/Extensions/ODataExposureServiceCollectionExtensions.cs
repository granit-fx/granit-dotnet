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
    /// Enables exactly the query features the per-route
    /// <c>ODataValidationSettings</c> can allow ($filter, $select, $orderby,
    /// $count, $expand, $skiptoken; $top and $skip carry no enable gate on
    /// <c>ODataMiniOptions</c> and are clamped per route) — per-set
    /// restrictions (expand whitelist + depth, count gate, orderby whitelist,
    /// max top) are applied per route.
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
        // minimal-API-only hosts. #3005: the former EnableAll() is replaced
        // by the explicit per-option list so nothing outside the validated
        // surface is ever switched on ($compute / $apply / $search have no
        // enable hook on ODataMiniOptions 9.4.x and stay off; the per-route
        // ODataValidationSettings.AllowedQueryOptions never includes them
        // either — belt and braces). SetMaxTop(null) mirrors EnableAll()'s
        // global no-cap default — the real cap is the per-route
        // SetMaxTop(descriptor.MaxTop) silent clamp pinned by
        // QueryHardeningTests.
        services.AddOData(opt => opt
            .Filter()
            .Select()
            .OrderBy()
            .Count()
            .SkipToken()
            .Expand()
            .SetMaxTop(null));

        // C3 hardening telemetry: counts rejected queries (count disabled,
        // expand not whitelisted) and top-clamps. Always on so observability
        // tooling can spot misconfigured BI refresh jobs without per-host
        // setup.
        services.TryAddSingleton<ODataExposureMetrics>();

        return services;
    }
}
