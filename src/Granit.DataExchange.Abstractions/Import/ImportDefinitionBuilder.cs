using System.Linq.Expressions;

namespace Granit.DataExchange.Import;

/// <summary>
/// Fluent builder for declaring importable properties, business keys, and grouping
/// within an <see cref="ImportDefinition{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public sealed class ImportDefinitionBuilder<TEntity> where TEntity : class
{
    internal List<PropertyMapping> Properties { get; } = [];
    internal List<string> BusinessKeyProperties { get; } = [];
    internal List<string> ExcludedOnUpdateProperties { get; } = [];
    internal bool HasExternalIdFlag { get; private set; }
    internal string? GroupByColumn { get; private set; }

    /// <summary>
    /// Declares an importable property on the target entity.
    /// Only properties explicitly declared here are available for column mapping.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public ImportDefinitionBuilder<TEntity> Property<TProp>(
        Expression<Func<TEntity, TProp>> property,
        Action<PropertyMappingBuilder>? configure = null)
    {
        string propertyName = GetPropertyName(property);
        PropertyMappingBuilder builder = new();
        configure?.Invoke(builder);

        Properties.Add(new PropertyMapping
        {
            PropertyPath = propertyName,
            ClrTypeName = typeof(TProp).Name,
            DisplayName = builder.DisplayNameValue,
            Description = builder.DescriptionValue,
            Aliases = builder.AliasValues.AsReadOnly(),
            IsRequired = builder.IsRequired,
            Format = builder.FormatValue,
        });

        return this;
    }

    /// <summary>
    /// Declares a business key property for roundtrip INSERT vs UPDATE resolution.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the business key property.</param>
    public ImportDefinitionBuilder<TEntity> HasBusinessKey<TProp>(
        Expression<Func<TEntity, TProp>> property)
    {
        BusinessKeyProperties.Add(GetPropertyName(property));
        return this;
    }

    /// <summary>
    /// Declares a composite business key for roundtrip resolution.
    /// </summary>
    /// <param name="properties">Expressions selecting the key properties.</param>
    public ImportDefinitionBuilder<TEntity> HasCompositeKey(
        params Expression<Func<TEntity, object?>>[] properties)
    {
        foreach (Expression<Func<TEntity, object?>> property in properties)
        {
            BusinessKeyProperties.Add(GetPropertyName(property));
        }

        return this;
    }

    /// <summary>
    /// Enables external ID-based identity resolution using a dedicated mapping table.
    /// A dedicated mapping column is added for the external identifier.
    /// </summary>
    public ImportDefinitionBuilder<TEntity> HasExternalId()
    {
        HasExternalIdFlag = true;
        return this;
    }

    /// <summary>
    /// Excludes a property from being overwritten during UPDATE operations.
    /// Useful for immutable fields like <c>CreatedAt</c>.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property to exclude on update.</param>
    public ImportDefinitionBuilder<TEntity> ExcludeOnUpdate<TProp>(
        Expression<Func<TEntity, TProp>> property)
    {
        ExcludedOnUpdateProperties.Add(GetPropertyName(property));
        return this;
    }

    /// <summary>
    /// Declares a group-by column for parent/child import.
    /// Consecutive rows sharing the same value in this column are grouped into a single entity.
    /// The file must be sorted by this column.
    /// </summary>
    /// <param name="columnName">The source column name to group by.</param>
    public ImportDefinitionBuilder<TEntity> GroupBy(string columnName)
    {
        GroupByColumn = columnName;
        return this;
    }

    /// <summary>
    /// Declares a child collection property for parent/child import.
    /// Each row in a group maps to one child entity.
    /// Requires <see cref="GroupBy"/> to be set.
    /// </summary>
    /// <typeparam name="TChild">The child entity type.</typeparam>
    /// <param name="collection">Expression selecting the collection property.</param>
    /// <param name="configure">Configuration for the child properties.</param>
    public ImportDefinitionBuilder<TEntity> HasMany<TChild>(
        Expression<Func<TEntity, IEnumerable<TChild>>> collection,
        Action<ChildCollectionBuilder<TChild>> configure)
        where TChild : class
    {
        string collectionName = GetPropertyName(collection);
        ChildCollectionBuilder<TChild> childBuilder = new(collectionName);
        configure(childBuilder);
        Properties.AddRange(childBuilder.Properties);
        return this;
    }

    private static string GetPropertyName<TProp>(Expression<Func<TEntity, TProp>> expression)
    {
        MemberExpression? member = expression.Body switch
        {
            MemberExpression m => m,
            UnaryExpression { Operand: MemberExpression m } => m,
            _ => null,
        };

        if (member is null || member.Expression is not ParameterExpression)
        {
            throw new ArgumentException(
                "Expression must be a simple property access (e.g. x => x.Name).",
                nameof(expression));
        }

        return member.Member.Name;
    }
}
