using System.Linq.Expressions;

namespace Granit.DataExchange.Export;

/// <summary>
/// Fluent builder for declaring exportable fields within an <see cref="ExportDefinition{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The source entity type.</typeparam>
/// <remarks>
/// <para>
/// Only fields explicitly declared here are available for export — this acts as a
/// security whitelist.
/// </para>
/// <para>
/// Navigation fields (<c>Field(e => e.Company, c => c.Name)</c>) use dot notation.
/// The developer <b>must</b> call the necessary <c>Include()</c> in
/// <c>IExportDataSource{TEntity}</c>. No auto-include magic in V1.
/// </para>
/// </remarks>
public sealed class ExportDefinitionBuilder<TEntity> where TEntity : class
{
    private int _autoOrder;

    internal List<ExportFieldDescriptor> Fields { get; } = [];
    internal bool IncludeIdFlag { get; private set; }
    internal bool IncludeBusinessKeyFlag { get; private set; }
    internal bool IncludeMetadataFlag { get; private set; }

    /// <summary>
    /// Declares a simple exportable field on the entity.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public ExportDefinitionBuilder<TEntity> Field<TProp>(
        Expression<Func<TEntity, TProp>> property,
        Action<ExportFieldBuilder>? configure = null)
    {
        string propertyPath = GetPropertyName(property);
        ExportFieldBuilder builder = new();
        configure?.Invoke(builder);

        Fields.Add(new ExportFieldDescriptor(
            PropertyPath: propertyPath,
            ClrTypeName: typeof(TProp).Name,
            Header: builder.HeaderValue,
            Format: builder.FormatValue,
            Order: builder.OrderValue != 0 ? builder.OrderValue : _autoOrder++,
            IsNavigation: false));

        return this;
    }

    /// <summary>
    /// Declares a navigation field (dot notation) for relational traversal.
    /// </summary>
    /// <typeparam name="TNav">The navigation property type.</typeparam>
    /// <typeparam name="TProp">The target property type on the navigation.</typeparam>
    /// <param name="navigation">Expression selecting the navigation property.</param>
    /// <param name="property">Expression selecting the target property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    /// <remarks>
    /// The developer must ensure the corresponding <c>Include()</c> is present in
    /// <c>IExportDataSource{TEntity}</c>. If not, the value will be <c>null</c>.
    /// </remarks>
    public ExportDefinitionBuilder<TEntity> Field<TNav, TProp>(
        Expression<Func<TEntity, TNav?>> navigation,
        Expression<Func<TNav, TProp>> property,
        Action<ExportFieldBuilder>? configure = null)
    {
        string navPath = GetPropertyName(navigation);
        string propName = GetPropertyName(property);
        string fullPath = $"{navPath}.{propName}";

        ExportFieldBuilder builder = new();
        configure?.Invoke(builder);

        Fields.Add(new ExportFieldDescriptor(
            PropertyPath: fullPath,
            ClrTypeName: typeof(TProp).Name,
            Header: builder.HeaderValue,
            Format: builder.FormatValue,
            Order: builder.OrderValue != 0 ? builder.OrderValue : _autoOrder++,
            IsNavigation: true));

        return this;
    }

    /// <summary>
    /// Includes the entity <c>Id</c> column in exports for roundtrip import compatibility.
    /// </summary>
    public ExportDefinitionBuilder<TEntity> IncludeId()
    {
        IncludeIdFlag = true;
        return this;
    }

    /// <summary>
    /// Includes business key columns (from the matching <c>ImportDefinition{TEntity}</c>)
    /// for roundtrip import resolution.
    /// </summary>
    public ExportDefinitionBuilder<TEntity> IncludeBusinessKey()
    {
        IncludeBusinessKeyFlag = true;
        return this;
    }

    /// <summary>
    /// Declares a complex (hierarchical) exportable field backed by an arbitrary value selector.
    /// </summary>
    /// <typeparam name="TValue">The type of the complex value (object, collection, tree).</typeparam>
    /// <param name="name">
    /// Field name used as the <see cref="ExportFieldDescriptor.PropertyPath"/> and default column header.
    /// Must be a valid identifier — dots are not allowed (use a flat name for the complex payload).
    /// </param>
    /// <param name="selector">Selector that extracts the complex value from the entity.</param>
    /// <param name="configure">Optional fluent configuration (header, order).</param>
    /// <remarks>
    /// <para>
    /// Unlike scalar <see cref="Field{TProp}(System.Linq.Expressions.Expression{System.Func{TEntity,TProp}},System.Action{ExportFieldBuilder}?)"/>,
    /// <c>ComplexField</c> accepts any <c>Func&lt;TEntity, TValue&gt;</c> — no member-expression restriction.
    /// </para>
    /// <para>
    /// The resulting field sets <see cref="ExportFieldDescriptor.RequiresHierarchy"/> to <c>true</c>.
    /// Export writers that do not declare <c>SupportsHierarchy = true</c> in their capabilities
    /// will throw <see cref="Exceptions.ExportProviderIncompatibleException"/> (default) or skip the field
    /// depending on <see cref="ExportDefinition{TEntity}.OnIncompatibleField"/>.
    /// </para>
    /// </remarks>
    public ExportDefinitionBuilder<TEntity> ComplexField<TValue>(
        string name,
        Func<TEntity, TValue> selector,
        Action<ExportFieldBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(selector);

        ExportFieldBuilder builder = new();
        configure?.Invoke(builder);

        Func<object, object?> boxedSelector = entity => selector((TEntity)entity);

        Fields.Add(new ExportFieldDescriptor(
            PropertyPath: name,
            ClrTypeName: typeof(TValue).Name,
            Header: builder.HeaderValue,
            Format: builder.FormatValue,
            Order: builder.OrderValue != 0 ? builder.OrderValue : _autoOrder++,
            IsNavigation: false,
            RequiresHierarchy: true,
            ValueSelector: boxedSelector,
            SelectorType: typeof(TValue)));

        return this;
    }

    /// <summary>
    /// Includes mapped extra properties (from <c>IMetadataMappingRegistry</c>)
    /// as additional export fields after the explicitly declared fields.
    /// </summary>
    /// <remarks>
    /// Only applicable to entities implementing <c>IHasMetadata</c> that have
    /// extra properties mapped via <c>MapProperty&lt;T&gt;()</c>. The extra fields are
    /// discovered at runtime by <see cref="IExtraExportFieldProvider"/> and their values
    /// are resolved by <see cref="IExportExtraValueResolver"/>.
    /// </remarks>
    public ExportDefinitionBuilder<TEntity> IncludeMetadata()
    {
        IncludeMetadataFlag = true;
        return this;
    }

    private static string GetPropertyName<TSource, TProp>(Expression<Func<TSource, TProp>> expression)
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
