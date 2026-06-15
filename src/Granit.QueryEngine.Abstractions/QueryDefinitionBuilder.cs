using System.Linq.Expressions;
using Granit.Domain;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Options;
using Granit.QueryEngine.Search;

namespace Granit.QueryEngine;

/// <summary>
/// Fluent builder for declaring columns, filter groups, pagination, sorting, and global search
/// within a <see cref="QueryDefinition{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public sealed class QueryDefinitionBuilder<TEntity> where TEntity : class
{
    /// <summary>
    /// Initializes a new builder with default QueryEngine options.
    /// </summary>
    public QueryDefinitionBuilder() : this(new QueryEngineOptions()) { }

    internal QueryDefinitionBuilder(QueryEngineOptions options)
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
    internal LambdaExpression? ProjectionExpression { get; private set; }
    internal Type? ProjectionType { get; private set; }
    internal LookupSourceDescriptor? LookupSourceValue { get; private set; }

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
            LabelKey = builder.LabelKeyValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
            Lookup = builder.LookupValue,
            CurrencyCode = builder.CurrencyCodeValue,
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
            LabelKey = builder.LabelKeyValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
            Lookup = builder.LookupValue,
            CurrencyCode = builder.CurrencyCodeValue,
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
            LabelKey = builder.LabelKeyValue,
            Order = builder.OrderValue,
            IsSortable = builder.IsSortableValue,
            IsFilterable = builder.IsFilterableValue,
            IsVisible = builder.IsVisibleValue,
            Format = builder.FormatValue,
            Lookup = builder.LookupValue,
            CurrencyCode = builder.CurrencyCodeValue,
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
            // Reject a value-object column smuggled in via its implicit string operator
            // (e.g. a SingleValueObject<string> selected as `x => x.Host`): the compiler
            // wraps the member access in a Convert node whose operand is the non-string VO
            // type. Such a column is mapped by a ValueConverter, which EF Core treats as an
            // opaque whole-value round-trip — LIKE never translates, so the search term is
            // silently dropped and the whole (scoped) table is returned. Fail loud here
            // instead of shipping a no-op global search. See issue #2767.
            if (property.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert
                && convert.Operand.Type != typeof(string))
            {
                throw new ArgumentException(
                    $"GlobalSearch property '{GetPropertyName(property)}' has CLR type " +
                    $"'{convert.Operand.Type.Name}', not string. Value-object columns " +
                    $"(SingleValueObject<string>) cannot be substring-searched: EF Core cannot " +
                    $"translate LIKE over a ValueConverter, so the term would be silently ignored. " +
                    $"Use a plain string column for global search. See issue #2767.",
                    nameof(properties));
            }

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
        string propertyName = GetPropertyName(property);
        ThrowIfValueObjectColumn<TProp>(propertyName, "grouped by", nameof(property));

        GroupByFields.Add(new GroupByDescriptor
        {
            PropertyName = propertyName,
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
        string propertyName = GetPropertyName(property);
        ThrowIfValueObjectColumn<TProp>(propertyName, "aggregated", nameof(property));

        Aggregates.Add(new AggregateDescriptor
        {
            PropertyName = propertyName,
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
        string propertyName = GetPropertyName(property);
        ThrowIfValueObjectColumn<TProp>(propertyName, "used as a cursor key", nameof(property));

        CursorPropertyName = propertyName;
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

    /// <summary>
    /// Declares a server-side projection applied to every non-grouped query result.
    /// When set, <c>MapGranitQuery&lt;TEntity&gt;</c> returns <c>PagedResult&lt;TDto&gt;</c>
    /// instead of <c>PagedResult&lt;TEntity&gt;</c>, reducing I/O by selecting only the
    /// requested columns at the database level.
    /// </summary>
    /// <typeparam name="TDto">The projected DTO type.</typeparam>
    /// <param name="projection">
    /// Projection expression. Must be translatable to SQL by EF Core — non-translatable
    /// expressions (e.g. calls to arbitrary C# methods) will throw at runtime.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown if <c>ProjectTo</c> is called more than once.</exception>
    public QueryDefinitionBuilder<TEntity> ProjectTo<TDto>(Expression<Func<TEntity, TDto>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (ProjectionExpression is not null)
        {
            throw new InvalidOperationException(
                "ProjectTo has already been configured for this query definition.");
        }

        ProjectionExpression = projection;
        ProjectionType = typeof(TDto);
        return this;
    }

    /// <summary>
    /// Declares that this query definition doubles as a data-lookup source — a typeahead
    /// picker registered under <paramref name="name"/> in <c>Granit.DataLookup</c>. The
    /// generated <c>QueryDefinitionLookupSource&lt;TEntity&gt;</c> reuses this definition's
    /// global search, default sort, and pagination (including keyset/cursor) via
    /// <see cref="IQueryEngine{TEntity}"/>, projecting each row to <c>{ value, label }</c>.
    /// </summary>
    /// <typeparam name="TValue">The lookup value type (typically the entity key).</typeparam>
    /// <param name="name">Unique lookup registry key (e.g. <c>"tenants"</c>).</param>
    /// <param name="value">Selector for the lookup value (typically the primary key).</param>
    /// <param name="label">Selector for the human-readable, already-localized label.</param>
    /// <param name="requiredPermission">Optional permission the caller must hold.</param>
    /// <param name="scopeKeys">
    /// Scope keys the caller must supply. Each SHOULD match a filterable column so its value
    /// is applied as an equality filter (cascading pickers).
    /// </param>
    public QueryDefinitionBuilder<TEntity> AsLookup<TValue>(
        string name,
        Expression<Func<TEntity, TValue>> value,
        Expression<Func<TEntity, string>> label,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(label);

        var boxed = Expression.Lambda<Func<TEntity, object>>(
            Expression.Convert(value.Body, typeof(object)),
            value.Parameters);

        LookupSourceValue = new LookupSourceDescriptor
        {
            Name = name,
            ValueSelector = value,
            BoxedValueSelector = boxed,
            LabelSelector = label,
            ValueType = typeof(TValue),
            RequiredPermission = requiredPermission,
            ScopeKeys = scopeKeys ?? [],
        };

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
            // A nested access such as `x => x.Host.Value` (drilling into a SingleValueObject
            // to reach its primitive) is the common cause: the value object is mapped as a
            // whole-value ValueConverter, so the inner `.Value` is not an independently
            // queryable column. Point the author at the real constraint rather than emitting
            // an opaque error. See issue #2767.
            string hint = member?.Expression is MemberExpression inner
                ? $" Nested access like 'x => x.{inner.Member.Name}.{member.Member.Name}' is not " +
                  $"supported; if '{inner.Member.Name}' is a SingleValueObject, value-object columns " +
                  $"cannot be substring-searched or filtered (see issue #2767) — select a plain " +
                  $"string column instead."
                : string.Empty;

            throw new ArgumentException(
                "Expression must be a simple property access (e.g. x => x.Name)." + hint,
                nameof(expression));
        }

        return member.Member.Name;
    }

    // Rejects a SingleValueObject<T> column on paths that need an orderable/aggregatable scalar.
    // The value object is mapped as an opaque whole-value ValueConverter, so cursor keyset
    // comparisons, GROUP BY keys and aggregate functions over it either cannot translate or are
    // meaningless. Equality/IN filtering and sorting remain supported. See issue #2767.
    private static void ThrowIfValueObjectColumn<TProp>(string propertyName, string operation, string paramName)
    {
        Type openType = typeof(SingleValueObject<>);
        for (Type? current = typeof(TProp); current is not null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openType)
            {
                throw new ArgumentException(
                    $"Property '{propertyName}' is a value object ({typeof(TProp).Name}) and cannot be " +
                    $"{operation}: it is mapped as an opaque whole-value ValueConverter (no orderable/" +
                    $"aggregatable scalar). Use a plain scalar column instead. See issue #2767.",
                    paramName);
            }
        }
    }
}
