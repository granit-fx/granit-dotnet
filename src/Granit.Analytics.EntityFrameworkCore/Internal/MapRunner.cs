using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Diagnostics;
using Granit.Analytics.Internal;
using Granit.MultiTenancy;
using Granit.QueryEngine;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Typed implementation of <see cref="IMapRunner"/> — closes over
/// <typeparamref name="TEntity"/> so dispatch by query name stays
/// reflection-free at request time. Streams the filtered entity set through
/// <see cref="IQueryEngine{TEntity}.ExecuteStreamAsync"/> and projects each
/// row into a <see cref="MapRunnerPoint"/> by reflecting the latitude /
/// longitude columns and (optionally) the popup columns.
/// </summary>
/// <remarks>
/// Coordinate validation happens here, not in the renderer: rows whose
/// latitude / longitude fall outside the WGS84 bounds (or are
/// <see cref="double.NaN"/> / <see cref="double.IsInfinity(double)"/>) are
/// silently dropped and counted on
/// <c>granit.analytics.map.invalid_coordinates</c>. Per B7 acceptance — bad
/// data must NOT break the widget; the dashboard must keep rendering.
/// </remarks>
internal sealed class MapRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine,
    AnalyticsRuntimeMetrics metrics,
    ICurrentTenant? currentTenant = null,
    IGeographyPointProjector<TEntity>? geographyProjector = null) : IMapRunner
    where TEntity : class
{
    private const double LatitudeMin = -90d;
    private const double LatitudeMax = 90d;
    private const double LongitudeMin = -180d;
    private const double LongitudeMax = 180d;

    internal const string ReasonNonFinite = "non_finite";
    internal const string ReasonLatitudeOutOfRange = "latitude_out_of_range";
    internal const string ReasonLongitudeOutOfRange = "longitude_out_of_range";

    private static readonly JsonSerializerOptions PopupJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;
    private readonly AnalyticsRuntimeMetrics _metrics = metrics;
    private readonly ICurrentTenant? _currentTenant = currentTenant;
    private readonly IGeographyPointProjector<TEntity>? _geographyProjector = geographyProjector;

    public string Name { get; } = name;

    public bool SupportsGeography => _geographyProjector is not null;

    public async Task<MapRunnerResult> ExecuteAsync(
        MapPointSource pointSource,
        IReadOnlyList<string>? popupColumns,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pointSource);

        // Resolve the per-row coordinate extractor once, before streaming.
        // LatLng path = reflect on two double/decimal columns; Geography path
        // = delegate to the registered IGeographyPointProjector. Throwing
        // NotSupportedException on Geography-without-projector is a safety
        // net — renderers MUST pre-check SupportsGeography to surface the
        // localised Unavailable instead.
        Func<TEntity, (double Latitude, double Longitude)?> extractor = pointSource switch
        {
            MapPointSource.LatLng latLng => BuildLatLngExtractor(latLng),
            MapPointSource.Geography geography => BuildGeographyExtractor(geography),
            _ => throw new NotSupportedException(
                $"MapPointSource '{pointSource.GetType().Name}' is not supported by MapRunner<{typeof(TEntity).Name}>."),
        };

        PropertyInfo? idProp = typeof(TEntity).GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance);
        // Only treat Id as the marker key when it's a Guid — non-Guid Id
        // columns (string codes, ints) are domain-specific and surface
        // through the popup payload when included there.
        if (idProp is not null && idProp.PropertyType != typeof(Guid))
        {
            idProp = null;
        }

        PropertyInfo[] popupProps = popupColumns is { Count: > 0 }
            ? ResolvePopupProperties(popupColumns)
            : [];

        QueryRequest request = new()
        {
            Filter = DashboardFilterTranslator.ToQueryRequestFilter(dashboardFilters),
        };

        List<MapRunnerPoint> points = [];

        await foreach (TEntity entity in _engine
            .ExecuteStreamAsync(_source.GetQueryable(), request, cancellationToken)
            .ConfigureAwait(false))
        {
            // Skip rows whose coordinate source yields nothing — null lat/lng
            // pair on LatLng path, or null Point on Geography path. The marker
            // would be pinned to (0, 0) which is misleading. Frontend never
            // sees them.
            if (extractor(entity) is not { } coords)
            {
                continue;
            }

            if (TryGetInvalidReason(coords.Latitude, coords.Longitude) is { } reason)
            {
                _metrics.RecordMapInvalidCoordinate(ResolveTenantTag(), reason);
                continue;
            }

            Guid? id = idProp is not null
                ? (Guid)(idProp.GetValue(entity) ?? Guid.Empty)
                : null;

            JsonElement? popup = popupProps.Length > 0
                ? BuildPopup(entity, popupProps)
                : null;

            points.Add(new MapRunnerPoint(id, coords.Latitude, coords.Longitude, popup));
        }

        return new MapRunnerResult(points);
    }

    /// <summary>Builds the LatLng-path coordinate extractor — reflects two double/decimal columns and converts via invariant culture.</summary>
    private static Func<TEntity, (double Latitude, double Longitude)?> BuildLatLngExtractor(MapPointSource.LatLng latLng)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(latLng.LatitudeColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(latLng.LongitudeColumn);

        PropertyInfo latProp = ResolveCoordinateProperty(latLng.LatitudeColumn, nameof(latLng.LatitudeColumn));
        PropertyInfo lngProp = ResolveCoordinateProperty(latLng.LongitudeColumn, nameof(latLng.LongitudeColumn));

        return entity =>
        {
            object? latRaw = latProp.GetValue(entity);
            object? lngRaw = lngProp.GetValue(entity);

            if (latRaw is null || lngRaw is null)
            {
                return null;
            }

            double latitude = Convert.ToDouble(latRaw, CultureInfo.InvariantCulture);
            double longitude = Convert.ToDouble(lngRaw, CultureInfo.InvariantCulture);
            return (latitude, longitude);
        };
    }

    /// <summary>Builds the Geography-path coordinate extractor — delegates to the registered <see cref="IGeographyPointProjector{TEntity}"/>.</summary>
    private Func<TEntity, (double Latitude, double Longitude)?> BuildGeographyExtractor(MapPointSource.Geography geography)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geography.GeographyColumn);

        if (_geographyProjector is null)
        {
            throw new NotSupportedException(
                $"MapRunner<{typeof(TEntity).Name}> received a Geography PointSource but no IGeographyPointProjector<{typeof(TEntity).Name}> is registered. " +
                "Renderers should pre-check IMapRunner.SupportsGeography and surface 'Widget:Unavailable.MapGeographyNotImplemented' instead.");
        }

        IGeographyPointProjector<TEntity> projector = _geographyProjector;
        string column = geography.GeographyColumn;
        return entity => projector.TryProject(entity, column);
    }

    private static PropertyInfo ResolveCoordinateProperty(string fieldName, string paramName)
    {
        PropertyInfo prop = ResolveProperty(fieldName, paramName);
        Type underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (underlying != typeof(double) && underlying != typeof(decimal))
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"Coordinate column '{prop.Name}' on entity '{typeof(TEntity).Name}' is of type '{underlying.Name}'. Map widgets require double or decimal latitude/longitude columns."),
                paramName);
        }
        return prop;
    }

    private static PropertyInfo[] ResolvePopupProperties(IReadOnlyList<string> popupColumns)
    {
        var resolved = new PropertyInfo[popupColumns.Count];
        for (int i = 0; i < popupColumns.Count; i++)
        {
            resolved[i] = ResolveProperty(popupColumns[i], nameof(popupColumns));
        }
        return resolved;
    }

    private static PropertyInfo ResolveProperty(string fieldName, string paramName)
    {
        PropertyInfo? prop = typeof(TEntity).GetProperty(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return prop ?? throw new ArgumentException(
            $"Field '{fieldName}' not found on entity '{typeof(TEntity).Name}'.",
            paramName);
    }

    /// <summary>
    /// Returns the snake_case reason tag when <paramref name="latitude"/> /
    /// <paramref name="longitude"/> fall outside WGS84 bounds or are non-finite;
    /// <see langword="null"/> when the coordinates are valid.
    /// </summary>
    private static string? TryGetInvalidReason(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            return ReasonNonFinite;
        }

        if (latitude is < LatitudeMin or > LatitudeMax)
        {
            return ReasonLatitudeOutOfRange;
        }

        if (longitude is < LongitudeMin or > LongitudeMax)
        {
            return ReasonLongitudeOutOfRange;
        }

        return null;
    }

    private string? ResolveTenantTag() =>
        _currentTenant is { IsAvailable: true, Id: { } id }
            ? id.ToString()
            : null;

    private static JsonElement BuildPopup(TEntity entity, PropertyInfo[] popupProps)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (PropertyInfo prop in popupProps)
        {
            dict[prop.Name] = prop.GetValue(entity);
        }
        return JsonSerializer.SerializeToElement(dict, PopupJsonOptions);
    }
}
