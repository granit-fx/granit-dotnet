namespace Granit.DataExchange.Export;

/// <summary>
/// Resolves the value of a mapped extra property on an entity instance.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation reads from the <c>ExtraPropertiesJson</c> bag,
/// which only contains <b>unmapped</b> properties. The EF Core implementation
/// reads from shadow properties via <c>DbContext.Entry()</c>, which contains
/// the actual column values for mapped properties.
/// </para>
/// <para>
/// When <c>Granit.DataExchange.EntityFrameworkCore</c> is loaded, the EF Core
/// implementation replaces the default.
/// </para>
/// </remarks>
public interface IExportExtraValueResolver
{
    /// <summary>
    /// Resolves the value of a mapped extra property.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The extra property name.</param>
    /// <returns>The property value, or <see langword="null"/> if not available.</returns>
    object? ResolveExtraValue(object entity, string propertyName);
}
