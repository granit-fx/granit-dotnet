using System.Globalization;
using System.Reflection;
using System.Text.Json;
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
internal sealed class MapRunner<TEntity>(
    string name,
    IQueryableSource<TEntity> source,
    IQueryEngine<TEntity> engine) : IMapRunner
    where TEntity : class
{
    private static readonly JsonSerializerOptions PopupJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IQueryEngine<TEntity> _engine = engine;

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
