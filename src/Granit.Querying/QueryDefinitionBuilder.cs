using System.Linq.Expressions;
using Granit.Querying.Filtering;
using Granit.Querying.Options;
using Granit.Querying.Search;

namespace Granit.Querying;

/// <summary>
/// Fluent builder for declaring columns, filter groups, pagination, sorting, and global search
/// within a <see cref="QueryDefinition{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public sealed class QueryDefinitionBuilder<TEntity> where TEntity : class
{
    /// <summary>
    /// Initializes a new builder with default querying options.
    /// </summary>
    public QueryDefinitionBuilder() : this(new QueryingOptions()) { }

    internal QueryDefinitionBuilder(QueryingOptions options)
    {
        DefaultPageSizeValue = options.DefaultPageSize;
        MaxPageSizeValue = options.MaxPageSize;
        MaxStreamSizeValue = options.MaxStreamSize;
    }

    internal List<ColumnDescriptor> Columns { get; } = [];
    internal List<FilterGroupDescriptor> FilterGroups { get; } = [];
    internal List<DateFilterDescriptor> DateFilters { get; } = [];
    internal List<GroupByDescriptor> GroupByFields { get; } = [];
    internal List<AggregateDescriptor> Aggregates { get; } = [];
    internal List<QuickFilterDescriptor> QuickFilters { get; } = [];
    internal List<string> GlobalSearchProperties { get; } = [];
    internal int DefaultPageSizeValue { get; private set; }
    internal int MaxPageSizeValue { get; private set; }
    internal int MaxStreamSizeValue { get; private set; }
    internal string? CursorPropertyName { get; private set; }
    internal Type? GlobalSearchStrategyType { get; private set; }
    internal string? DefaultSortValue { get; private set; }

    /// <summary>
    /// Declares a column on the target entity. Only explicitly declared columns are
    /// exposed to the frontend (whitelist-first).
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public QueryDefinitionBuilder<TEntity> Column<TProp>(
        Expression<Func<TEntity, TProp>> property,
        Action<ColumnBuilder<TEntity>>? configure = null)
    {
        string propertyName = GetPropertyName(property);
        ColumnBuilder<TEntity> builder = new();
        configure?.Invoke(builder);

        Columns.Add(new ColumnDescriptor
        {
            PropertyName = propertyName,
            ClrType = typeof(TProp),
            Label = builder.LabelValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
        });

        return this;
    }

    /// <summary>
    /// Declares a Shadow Property column (EF Core-only, no CLR property).
    /// Shadow columns are accessed via <c>EF.Property&lt;T&gt;(entity, name)</c> and
    /// support filtering and sorting when declared as such.
    /// </summary>
    /// <typeparam name="TProp">The CLR type of the shadow property.</typeparam>
    /// <param name="propertyName">The shadow property name (as declared in the EF model).</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public QueryDefinitionBuilder<TEntity> ShadowColumn<TProp>(
        string propertyName,
        Action<ColumnBuilder<TEntity>>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ColumnBuilder<TEntity> builder = new();
        configure?.Invoke(builder);

        Columns.Add(new ColumnDescriptor
        {
            PropertyName = propertyName,
            ClrType = typeof(TProp),
            Label = builder.LabelValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
            IsShadowProperty = true,
        });

        return this;
    }

    /// <summary>
    /// Declares a Shadow Property column with a runtime CLR type.
    /// Use this overload when the type is only known at runtime (e.g., from configuration).
    /// </summary>
    /// <param name="propertyName">The shadow property name (as declared in the EF model).</param>
    /// <param name="clrType">The CLR type of the shadow property.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    public QueryDefinitionBuilder<TEntity> ShadowColumn(
        string propertyName,
        Type clrType,
        Action<ColumnBuilder<TEntity>>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(clrType);
        ColumnBuilder<TEntity> builder = new();
        configure?.Invoke(builder);

        Columns.Add(new ColumnDescriptor
        {
            PropertyName = propertyName,
            ClrType = clrType,
            Label = builder.LabelValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
            IsShadowProperty = true,
        });

        return this;
    }

    /// <summary>
    /// Declares properties to include in global free-text search.
    /// </summary>
    /// <param name="properties">Expressions selecting string properties.</param>
    public QueryDefinitionBuilder<TEntity> GlobalSearch(
        params Expression<Func<TEntity, string?>>[] properties)
    {
        foreach (Expression<Func<TEntity, string?>> property in properties)
        {
            GlobalSearchProperties.Add(GetPropertyName(property));
        }

        return this;
    }

    /// <summary>
    /// Overrides the default global search strategy (<c>LIKE '%term%'</c>) with a custom
    /// implementation. Use this for provider-specific full-text search (e.g. PostgreSQL FTS).
    /// </summary>
    /// <typeparam name="TStrategy">
    /// The search strategy type. Must implement <see cref="IGlobalSearchStrategy{TEntity}"/>
    /// and be registered in DI.
    /// </typeparam>
    public QueryDefinitionBuilder<TEntity> UseSearchStrategy<TStrategy>()
        where TStrategy : class, IGlobalSearchStrategy<TEntity>
    {
        GlobalSearchStrategyType = typeof(TStrategy);
        return this;
    }

    /// <summary>
    /// Declares a filter group with named presets.
    /// Presets within a group use OR semantics; groups are combined with AND.
    /// </summary>
    /// <param name="name">Unique name of the filter group.</param>
    /// <param name="configure">Builder for declaring presets.</param>
    public QueryDefinitionBuilder<TEntity> FilterGroup(
        string name,
        Action<FilterGroupBuilder<TEntity>> configure)
    {
        FilterGroupBuilder<TEntity> builder = new();
        configure(builder);

        FilterGroups.Add(new FilterGroupDescriptor
        {
            Name = name,
            Presets = builder.Presets.AsReadOnly(),
        });

        return this;
    }

    /// <summary>
    /// Declares a filter group with a custom label and named presets.
    /// </summary>
    /// <param name="name">Unique name of the filter group.</param>
    /// <param name="label">User-facing label.</param>
    /// <param name="configure">Builder for declaring presets.</param>
    public QueryDefinitionBuilder<TEntity> FilterGroup(
        string name,
        string label,
        Action<FilterGroupBuilder<TEntity>> configure)
    {
        FilterGroupBuilder<TEntity> builder = new();
        configure(builder);

        FilterGroups.Add(new FilterGroupDescriptor
        {
            Name = name,
            Label = label,
            Presets = builder.Presets.AsReadOnly(),
        });

        return this;
    }

    /// <summary>
    /// Declares a date filter with period shortcuts on a date property.
    /// </summary>
    /// <typeparam name="TProp">The date property type.</typeparam>
    /// <param name="property">Expression selecting the date property.</param>
    /// <param name="defaultPeriod">The default date period. Default is <see cref="DatePeriod.ThisMonth"/>.</param>
    public QueryDefinitionBuilder<TEntity> DateFilter<TProp>(
        Expression<Func<TEntity, TProp>> property,
        DatePeriod defaultPeriod = DatePeriod.ThisMonth)
    {
        DateFilters.Add(new DateFilterDescriptor
        {
            PropertyName = GetPropertyName(property),
            ClrType = typeof(TProp),
            DefaultPeriod = defaultPeriod,
        });

        return this;
    }

    /// <summary>
    /// Allows grouping by the specified property. Only explicitly declared
    /// properties can be used for group-by (whitelist-first).
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property.</param>
    public QueryDefinitionBuilder<TEntity> AllowGroupBy<TProp>(
        Expression<Func<TEntity, TProp>> property)
    {
        GroupByFields.Add(new GroupByDescriptor
        {
            PropertyName = GetPropertyName(property),
            ClrType = typeof(TProp),
        });

        return this;
    }

    /// <summary>
    /// Declares an aggregate computation for grouped queries.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property to aggregate.</param>
    /// <param name="function">The aggregate function to apply.</param>
    /// <param name="alias">Alias for the aggregate result in the response.</param>
    public QueryDefinitionBuilder<TEntity> Aggregate<TProp>(
        Expression<Func<TEntity, TProp>> property,
        AggregateFunction function,
        string alias)
    {
        Aggregates.Add(new AggregateDescriptor
        {
            PropertyName = GetPropertyName(property),
            ClrType = typeof(TProp),
            Function = function,
            Alias = alias,
        });

        return this;
    }

    /// <summary>
    /// Declares an independent toggleable filter (quick filter).
    /// Unlike filter group presets which are mutually exclusive (OR within a group),
    /// quick filters are independently activatable and combine with AND semantics.
    /// </summary>
    /// <param name="name">Unique name of the filter (e.g. <c>"MyAppointments"</c>).</param>
    /// <param name="predicate">The predicate expression to apply when active.</param>
    /// <param name="isDefault">Whether this filter is active by default.</param>
    public QueryDefinitionBuilder<TEntity> QuickFilter(
        string name,
        Expression<Func<TEntity, bool>> predicate,
        bool isDefault = false)
    {
        QuickFilters.Add(new QuickFilterDescriptor
        {
            Name = name,
            Predicate = predicate,
            IsDefault = isDefault,
        });

        return this;
    }

    /// <summary>
    /// Declares an independent toggleable filter with a custom label.
    /// </summary>
    /// <param name="name">Unique name of the filter.</param>
    /// <param name="label">User-facing label (e.g. <c>"Mes rendez-vous"</c>).</param>
    /// <param name="predicate">The predicate expression to apply when active.</param>
    /// <param name="isDefault">Whether this filter is active by default.</param>
    public QueryDefinitionBuilder<TEntity> QuickFilter(
        string name,
        string label,
        Expression<Func<TEntity, bool>> predicate,
        bool isDefault = false)
    {
        QuickFilters.Add(new QuickFilterDescriptor
        {
            Name = name,
            Label = label,
            Predicate = predicate,
            IsDefault = isDefault,
        });

        return this;
    }

    /// <summary>
    /// Sets the default page size. Default is <c>20</c>.
    /// </summary>
    /// <param name="size">The default page size.</param>
    public QueryDefinitionBuilder<TEntity> DefaultPageSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        DefaultPageSizeValue = size;
        return this;
    }

    /// <summary>
    /// Sets the maximum allowed page size. Default is <c>100</c>.
    /// </summary>
    /// <param name="size">The maximum page size.</param>
    public QueryDefinitionBuilder<TEntity> MaxPageSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        MaxPageSizeValue = size;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of items returned by streaming queries
    /// (<see cref="IQueryEngine{TEntity}.ExecuteStreamAsync"/>). Default is <c>100_000</c>.
    /// </summary>
    /// <param name="size">The maximum stream size.</param>
    public QueryDefinitionBuilder<TEntity> MaxStreamSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        MaxStreamSizeValue = size;
        return this;
    }

    /// <summary>
    /// Enables keyset/cursor pagination on the specified property.
    /// The property must be unique and orderable (typically the primary key).
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the cursor property.</param>
    public QueryDefinitionBuilder<TEntity> SupportsCursorPagination<TProp>(
        Expression<Func<TEntity, TProp>> property)
    {
        CursorPropertyName = GetPropertyName(property);
        return this;
    }

    /// <summary>
    /// Sets the default sort specification (e.g. <c>"-createdAt"</c>).
    /// </summary>
    /// <param name="sort">The default sort string.</param>
    public QueryDefinitionBuilder<TEntity> DefaultSort(string sort)
    {
        DefaultSortValue = sort;
        return this;
    }

    internal static string GetPropertyName<TProp>(Expression<Func<TEntity, TProp>> expression)
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
