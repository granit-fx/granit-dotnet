namespace Granit.Analytics.Dashboards.Widgets;

/// <summary>
/// Projects a single geography column on <typeparamref name="TEntity"/> to a
/// <c>(latitude, longitude)</c> pair. Implemented by provider-specific packages
/// (e.g. <c>Granit.Analytics.PostGIS</c> wraps NetTopologySuite's <c>Point</c>);
/// the base framework only declares the contract so the renderer dispatch site
/// stays provider-agnostic.
/// </summary>
/// <remarks>
/// <para>
/// The projector returns <see langword="null"/> when the row carries no
/// geometry (the column is null on this row). The
/// <see cref="MapWidgetDefinition"/>'s renderer treats null projections the
/// same as null lat/lng pairs — the row is silently dropped from the rendered
/// snapshot, no exception leaks to the dashboard payload.
/// </para>
/// <para>
/// Out-of-range / non-finite coordinates ARE NOT validated here — they flow
/// through to <c>MapRunner</c>'s shared validation pipeline so the
/// <c>granit.analytics.map.invalid_coordinates</c> counter increments
/// uniformly across LatLng and Geography paths (B7 acceptance, PR #1505).
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity exposed by the backing <c>QueryDefinition</c>.</typeparam>
public interface IGeographyPointProjector<in TEntity>
    where TEntity : class
{
    /// <summary>
    /// Extracts the <c>(latitude, longitude)</c> pair from <paramref name="entity"/>'s
    /// configured geography column. Returns <see langword="null"/> when the
    /// underlying geometry is null.
    /// </summary>
    /// <param name="entity">The row to project.</param>
    /// <param name="geographyColumn">Name of the geography property to read.</param>
    /// <exception cref="ArgumentException">The named property does not exist on <typeparamref name="TEntity"/> or is not a supported geography type.</exception>
    (double Latitude, double Longitude)? TryProject(TEntity entity, string geographyColumn);
}
