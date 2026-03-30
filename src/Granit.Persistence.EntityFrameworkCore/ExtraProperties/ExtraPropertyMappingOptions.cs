using Granit.Domain;

namespace Granit.Persistence.EntityFrameworkCore.ExtraProperties;

/// <summary>
/// Options for declaring extra properties that should be mapped as real SQL columns
/// (EF Core Shadow Properties) on an entity that implements <see cref="IHasExtraProperties"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type to extend.</typeparam>
/// <remarks>
/// <para>
/// Mapped properties are added as Shadow Properties in the EF Core model at startup.
/// They are indexable and queryable via SQL, unlike the JSON bag in
/// <see cref="IHasExtraProperties.ExtraPropertiesJson"/>.
/// </para>
/// <para>
/// At save time, the <see cref="ExtraPropertySyncInterceptor"/> moves values from
/// the JSON bag to the Shadow Properties and excludes them from
/// <see cref="IHasExtraProperties.ExtraPropertiesJson"/> to prevent data duplication.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.Configure&lt;ExtraPropertyMappingOptions&lt;ReferenceDataEntity&gt;&gt;(options =&gt;
/// {
///     options.MapProperty&lt;string&gt;("Alpha3Code", maxLength: 3);
///     options.MapProperty&lt;bool&gt;("IsEuMember");
/// });
/// </code>
/// </example>
#pragma warning disable S2326 // TEntity is an Options-pattern discriminator (Configure<ExtraPropertyMappingOptions<T>>)
public sealed class ExtraPropertyMappingOptions<TEntity>
#pragma warning restore S2326
    where TEntity : class, IHasExtraProperties
{
    /// <summary>
    /// Gets the list of property mappings.
    /// </summary>
    public List<ExtraPropertyMapping> Mappings { get; } = [];

    /// <summary>
    /// Maps a property as a real SQL column on the entity's table.
    /// </summary>
    /// <typeparam name="T">The CLR type of the property.</typeparam>
    /// <param name="name">The property name (used as column name and ExtraProperties key).</param>
    /// <param name="maxLength">Maximum string length (only for <see cref="string"/> properties).</param>
    /// <param name="isRequired">Whether the column is NOT NULL. Default: <see langword="false"/>.</param>
    /// <param name="isFilterable">Whether the property is filterable in QueryEngine. Default: <see langword="false"/>.</param>
    /// <param name="isSortable">Whether the property is sortable in QueryEngine. Default: <see langword="false"/>.</param>
    public void MapProperty<T>(
        string name,
        int? maxLength = null,
        bool isRequired = false,
        bool isFilterable = false,
        bool isSortable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Mappings.Add(new ExtraPropertyMapping(name, typeof(T), maxLength, isRequired, isFilterable, isSortable));
    }
}
