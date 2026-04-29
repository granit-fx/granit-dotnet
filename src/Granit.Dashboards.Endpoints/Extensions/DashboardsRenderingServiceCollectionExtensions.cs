using Granit.Dashboards.Endpoints.Rendering;
using Granit.Dashboards.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Dashboards.Endpoints.Extensions;

/// <summary>
/// DI registration for the framework-side <see cref="IWidgetInstanceRenderer"/>
/// implementations bundled with <c>Granit.Dashboards.Endpoints</c> — currently
/// the static-content kinds (<c>Markdown</c>, <c>Text</c>, <c>Image</c>) that
/// do not need analytics or persistence to render. KPI / Chart / Table / Pivot
/// renderers ship from <c>Granit.Analytics.Endpoints</c> on the same
/// registration surface, so a host wiring both packages ends up with the full
/// catalogue of renderers under one <see cref="IEnumerable{T}"/> at the
/// dashboard render endpoint.
/// </summary>
public static class DashboardsRenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bundled static-content renderers (<c>Markdown</c>,
    /// <c>Text</c>, <c>Image</c>). Each renderer registers itself as an
    /// <see cref="IWidgetInstanceRenderer"/> — the dashboard renderer keys on
    /// <see cref="IWidgetInstanceRenderer.WidgetType"/> at startup, so adding a
    /// new kind is purely additive (no central switch).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDashboardsContentWidgetRenderers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IWidgetInstanceRenderer, MarkdownWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, TextWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, ImageWidgetInstanceRenderer>();

        return services;
    }
}
