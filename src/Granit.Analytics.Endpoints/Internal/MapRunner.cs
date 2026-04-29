using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Granit.Analytics.Endpoints.Diagnostics;
using Granit.MultiTenancy;
using Granit.QueryEngine;

namespace Granit.Analytics.Endpoints.Internal;

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
    AnalyticsEndpointsMetrics metrics,
    ICurrentTenant? currentTenant = null) : IMapRunner
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
    private readonly AnalyticsEndpointsMetrics _metrics = metrics;
    private readonly ICurrentTenant? _currentTenant = currentTenant;

    public string Name { get; } = name;

    public async Task<MapRunnerResult> ExecuteAsync(
        string latitudeColumn,
        string longitudeColumn,
        IReadOnlyList<string>? popupColumns,
        IReadOnlyDictionary<string, string>? dashboardFilters,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(latitudeColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(longitudeColumn);

        PropertyInfo latProp = ResolveCoordinateProperty(latitudeColumn, nameof(latitudeColumn));
        PropertyInfo lngProp = ResolveCoordinateProperty(longitudeColumn, nameof(longitudeColumn));

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
            object? latRaw = latProp.GetValue(entity);
            object? lngRaw = lngProp.GetValue(entity);

            // Skip rows missing coordinates entirely — the marker would be
            // pinned to (0, 0) which is misleading. Frontend never sees them.
            if (latRaw is null || lngRaw is null)
            {
                continue;
            }

            double latitude = Convert.ToDouble(latRaw, CultureInfo.InvariantCulture);
            double longitude = Convert.ToDouble(lngRaw, CultureInfo.InvariantCulture);

            if (TryGetInvalidReason(latitude, longitude) is { } reason)
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

            points.Add(new MapRunnerPoint(id, latitude, longitude, popup));
        }

        return new MapRunnerResult(points);
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
