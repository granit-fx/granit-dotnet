using System.Linq.Expressions;

namespace Granit.DataExchange.Import;

/// <summary>
/// Fluent builder for configuring child collection properties in a parent/child import.
/// </summary>
/// <typeparam name="TChild">The child entity type.</typeparam>
public sealed class ChildCollectionBuilder<TChild> where TChild : class
{
    private readonly string _parentPropertyPath;
    internal List<PropertyMapping> Properties { get; } = [];

    internal ChildCollectionBuilder(string parentPropertyPath) =>
        _parentPropertyPath = parentPropertyPath;

    /// <summary>
    /// Declares an importable property on the child entity.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public ChildCollectionBuilder<TChild> Property<TProp>(
        Expression<Func<TChild, TProp>> property,
        Action<PropertyMappingBuilder>? configure = null)
    {
        string propertyName = GetPropertyName(property);
        PropertyMappingBuilder builder = new();
        configure?.Invoke(builder);

        Properties.Add(new PropertyMapping
        {
            PropertyPath = $"{_parentPropertyPath}.{propertyName}",
            ClrTypeName = typeof(TProp).Name,
            DisplayName = builder.DisplayNameValue,
            Description = builder.DescriptionValue,
            Aliases = builder.AliasValues.AsReadOnly(),
            IsRequired = builder.IsRequired,
            Format = builder.FormatValue,
            IsChildCollection = true,
        });

        return this;
    }

    private static string GetPropertyName<TProp>(Expression<Func<TChild, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException(
            "Expression must be a simple property access (e.g. x => x.Name).",
            nameof(expression));
    }
}
