using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;

namespace Granit.Analytics.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// materialises geocoded query rows into map markers. One runner is registered
/// per query definition by <c>AddGranitAnalyticsRunners</c> at startup;
/// the dashboard render path resolves it by query name.
/// </summary>
/// <remarks>
/// <para>
/// Two coordinate flavours are dispatched internally based on the
/// <see cref="MapPointSource"/> argument: <see cref="MapPointSource.LatLng"/>
/// reflects two decimal columns (works on any database); the opt-in
/// <see cref="MapPointSource.Geography"/> path resolves a registered
/// <see cref="IGeographyPointProjector{TEntity}"/> to project a single PostGIS
/// <c>geography(Point)</c> column. <see cref="SupportsGeography"/> tells the
/// renderer whether the Geography path is wired up — when <see langword="false"/>,
/// the renderer surfaces <c>Widget:Unavailable.MapGeographyNotImplemented</c>
/// without invoking the runner.
/// </para>
/// <para>
/// Streams the filtered entity set through
/// <c>IQueryEngine.ExecuteStreamAsync</c> (multi-tenancy + soft-delete +
/// dashboard filters apply, cap at <c>MaxStreamSize</c>). Coordinate validation
/// is shared across both paths — invalid rows are dropped and counted on
/// <c>granit.analytics.map.invalid_coordinates</c>.
/// </para>
/// </remarks>
internal interface IMapRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// <see langword="true"/> when an <see cref="IGeographyPointProjector{TEntity}"/>
    /// is registered for the runner's entity type — the
    /// <see cref="MapPointSource.Geography"/> path is honoured. <see langword="false"/>
    /// when the runner is LatLng-only.
    /// </summary>
    bool SupportsGeography { get; }

    /// <summary>
    /// Loads geocoded rows as map markers. Each marker carries lat/lng
    /// coordinates, an optional entity id (read from a <c>Guid Id</c>
    /// property when present), and an optional popup payload restricted to
    /// the whitelisted <paramref name="popupColumns"/>.
    /// </summary>
    /// <param name="pointSource">Coordinate source — column pair (<see cref="MapPointSource.LatLng"/>) or PostGIS geography column (<see cref="MapPointSource.Geography"/>).</param>
    /// <param name="popupColumns">Whitelisted columns surfaced in the marker popup. <see langword="null"/> = no popup payload (only id + coords).</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the QueryDefinition's filter pipeline — see <see cref="DashboardFilterTranslator"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Column not declared on the entity or wrong primitive type.</exception>
    /// <exception cref="NotSupportedException"><paramref name="pointSource"/> is <see cref="MapPointSource.Geography"/> but <see cref="SupportsGeography"/> is <see langword="false"/>. Renderers should pre-check and surface <c>Unavailable</c> instead of letting this throw.</exception>
    Task<MapRunnerResult> ExecuteAsync(
        MapPointSource pointSource,
        IReadOnlyList<string>? popupColumns,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of an <see cref="IMapRunner.ExecuteAsync"/> call.</summary>
/// <param name="Points">Materialised map markers in stream-arrival order.</param>
internal sealed record MapRunnerResult(IReadOnlyList<MapRunnerPoint> Points);

/// <summary>One map marker.</summary>
/// <param name="Id">Entity primary key when the entity exposes a <c>Guid Id</c> property; <see langword="null"/> otherwise. Drives the click-through to <c>DetailRoute</c>.</param>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
/// <param name="Popup">Whitelisted column subset for the marker popup, serialised as a camelCase JSON object. <see langword="null"/> when no popup columns were requested.</param>
internal sealed record MapRunnerPoint(
    Guid? Id,
    double Latitude,
    double Longitude,
    JsonElement? Popup);
