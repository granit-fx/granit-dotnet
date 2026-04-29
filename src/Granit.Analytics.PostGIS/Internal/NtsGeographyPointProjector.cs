using System.Reflection;
using Granit.Analytics.Dashboards.Widgets;
using NetTopologySuite.Geometries;

namespace Granit.Analytics.PostGIS.Internal;

/// <summary>
/// <see cref="IGeographyPointProjector{TEntity}"/> implementation backed by
/// NetTopologySuite. Reads the named property on the entity, expects a
/// <see cref="Point"/> (the PostGIS <c>geography(Point)</c> mapping shipped by
/// <c>Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite</c>), and returns
/// <c>(latitude, longitude)</c> = <c>(Point.Y, Point.X)</c> per the WKT axis
/// order convention NTS uses.
/// </summary>
/// <remarks>
/// <para>
/// Cached <see cref="PropertyInfo"/> lookups per (entity type, column name)
/// keep the per-row reflection cost amortised — Map widgets typically iterate
/// thousands of rows but read at most one geography column per render.
/// </para>
/// <para>
/// Returning <see langword="null"/> on a null Point is intentional; the runner
/// then drops the row silently. Out-of-range / non-finite coordinates ARE NOT
/// validated here — that's <c>MapRunner</c>'s job (shared metric counter).
/// </para>
/// </remarks>
internal sealed class NtsGeographyPointProjector<TEntity> : IGeographyPointProjector<TEntity>
    where TEntity : class
{
    private readonly Dictionary<string, PropertyInfo> _propertyCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _cacheLock = new();

    public (double Latitude, double Longitude)? TryProject(TEntity entity, string geographyColumn)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(geographyColumn);

        PropertyInfo prop = ResolveProperty(geographyColumn);

        object? raw = prop.GetValue(entity);
        if (raw is null)
        {
            return null;
        }

        if (raw is not Point point)
        {
            throw new ArgumentException(
                $"Property '{prop.Name}' on entity '{typeof(TEntity).Name}' is of type '{raw.GetType().Name}'. " +
                $"NtsGeographyPointProjector requires a NetTopologySuite '{nameof(Point)}'. " +
                $"For PostGIS, configure Npgsql with 'UseNetTopologySuite()' and map the column as 'geography(Point)'.",
                nameof(geographyColumn));
        }

        // WKT axis order: X = longitude, Y = latitude.
        return (Latitude: point.Y, Longitude: point.X);
    }

    private PropertyInfo ResolveProperty(string column)
    {
        lock (_cacheLock)
        {
            if (_propertyCache.TryGetValue(column, out PropertyInfo? cached))
            {
                return cached;
            }

            PropertyInfo? prop = typeof(TEntity).GetProperty(
                column,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null)
            {
                throw new ArgumentException(
                    $"Property '{column}' not found on entity '{typeof(TEntity).Name}'.",
                    nameof(column));
            }

            _propertyCache[column] = prop;
            return prop;
        }
    }
}
