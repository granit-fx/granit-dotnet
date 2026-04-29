using System.Text.Json;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Non-generic façade over a typed <c>QueryDefinition&lt;TEntity&gt;</c> that
/// materialises geocoded query rows into map markers. One runner is registered
/// per query definition by <c>AddGranitAnalyticsWidgetRenderers</c> at startup;
/// the dashboard render path resolves it by query name.
/// </summary>
/// <remarks>
/// <para>
/// B7-2 ships the <c>MapPointSource.LatLng</c> path (decimal latitude /
/// longitude columns) — works on any database. The PostGIS
/// <c>MapPointSource.Geography</c> path is deferred (handled by the renderer
/// returning <c>Unavailable</c>) until <c>granit-iot</c> ships the
/// NetTopologySuite plumbing.
/// </para>
/// <para>
/// Streams the filtered entity set through
/// <c>IQueryEngine.ExecuteStreamAsync</c> (multi-tenancy + soft-delete +
/// dashboard filters apply, cap at <c>MaxStreamSize</c>). Acceptable for
/// dashboard tile contexts which are bounded data products.
/// </para>
/// </remarks>
internal interface IMapRunner
{
    /// <summary>The query definition's unique name.</summary>
    string Name { get; }

    /// <summary>
    /// Loads geocoded rows as map markers. Each marker carries lat/lng
    /// coordinates, an optional entity id (read from a <c>Guid Id</c>
    /// property when present), and an optional popup payload restricted to
    /// the whitelisted <paramref name="popupColumns"/>.
    /// </summary>
    /// <param name="latitudeColumn">Property name of the latitude column on the entity.</param>
    /// <param name="longitudeColumn">Property name of the longitude column on the entity.</param>
    /// <param name="popupColumns">Whitelisted columns surfaced in the marker popup. <see langword="null"/> = no popup payload (only id + coords).</param>
    /// <param name="dashboardFilters">Dashboard-level filter spec layered on top of the QueryDefinition's filter pipeline — see <see cref="DashboardFilterTranslator"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Latitude/longitude/popup column not declared on the entity, or coordinate column type is not <c>double</c> or <c>decimal</c>.</exception>
    Task<MapRunnerResult> ExecuteAsync(
        string latitudeColumn,
        string longitudeColumn,
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
