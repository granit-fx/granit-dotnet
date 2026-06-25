using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Extension methods for applying dynamic sorting to an <see cref="IQueryable{T}"/>.
/// </summary>
internal static class QueryableSortExtensions
{
    /// <summary>
    /// Parses a sort specification string and applies ordering.
    /// Format: <c>"-createdAt,lastName"</c> (prefix <c>-</c> for descending).
    /// Only whitelisted sortable columns are applied.
    /// </summary>
    /// <remarks>
    /// A deterministic ordering is <b>always</b> applied: when the request supplies no sort,
    /// the definition declares no <c>DefaultSort</c>, or none of the requested fields are
    /// whitelisted, a stable fallback key is used (the cursor key, then a conventional
    /// <c>Id</c>, then the first sortable column). Without it the downstream row-limiting
    /// operators (<c>Skip</c>/<c>Take</c>) run unordered, producing both EF Core's
    /// <c>RowLimitingOperationWithoutOrderByWarning</c> and non-deterministic pages.
    /// </remarks>
    public static IQueryable<TEntity> ApplySort<TEntity>(
        this IQueryable<TEntity> source,
        string? sort,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        string effectiveSort = string.IsNullOrWhiteSpace(sort)
            ? builder.DefaultSortValue ?? string.Empty
            : sort;

        bool isFirst = true;

        if (!string.IsNullOrWhiteSpace(effectiveSort))
        {
            var sortableFields = builder.Columns
                .Where(c => c.IsSortable)
                .Select(c => c.PropertyName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string[] parts = effectiveSort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string part in parts)
            {
                bool descending = part.StartsWith('-');
                string fieldName = descending ? part[1..] : part;

                if (!sortableFields.Contains(fieldName))
                {
                    continue;
                }

                // Check for shadow property first
                ColumnDescriptor? shadowCol = builder.Columns
                    .FirstOrDefault(c => c.IsShadowProperty
                        && string.Equals(c.PropertyName, fieldName, StringComparison.OrdinalIgnoreCase));

                if (shadowCol is not null)
                {
                    source = ApplyOrderByShadow(source, fieldName, shadowCol.ClrType, descending, isFirst);
                    isFirst = false;
                    continue;
                }

                PropertyInfo? property = typeof(TEntity).GetProperty(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property is null)
                {
                    continue;
                }

                source = ApplyOrderBy(source, property, descending, isFirst);
                isFirst = false;
            }
        }

        // No explicit ordering resolved — guarantee a deterministic order so that
        // Skip/Take downstream is stable and EF Core does not warn.
        return isFirst ? ApplyFallbackOrder(source, builder) : source;
    }

    /// <summary>
    /// Applies a deterministic fallback ordering when no explicit sort resolved.
    /// Prefers the cursor key (unique and orderable — typically the primary key), then a
    /// conventional <c>Id</c>, then the first sortable column. Returns the source unchanged
    /// only when none of these can be resolved to a CLR property.
    /// </summary>
    private static IQueryable<TEntity> ApplyFallbackOrder<TEntity>(
        IQueryable<TEntity> source,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        PropertyInfo? property = ResolveProperty<TEntity>(builder.CursorPropertyName)
            ?? ResolveProperty<TEntity>("Id")
            ?? builder.Columns
                .Where(c => c.IsSortable && !c.IsShadowProperty)
                .Select(c => ResolveProperty<TEntity>(c.PropertyName))
                .FirstOrDefault(p => p is not null);

        return property is null
            ? source
            : ApplyOrderBy(source, property, descending: false, isFirst: true);
    }

    private static PropertyInfo? ResolveProperty<TEntity>(string? name) =>
        string.IsNullOrEmpty(name)
            ? null
            : typeof(TEntity).GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static IQueryable<TEntity> ApplyOrderByShadow<TEntity>(
        IQueryable<TEntity> source,
        string propertyName,
        Type clrType,
        bool descending,
        bool isFirst)
        where TEntity : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");

        // EF.Property<T>(e, "PropertyName")
        MethodInfo efPropertyMethod = typeof(EF)
            .GetMethod(nameof(EF.Property))!
            .MakeGenericMethod(clrType);

        MethodCallExpression member = Expression.Call(efPropertyMethod, parameter, Expression.Constant(propertyName));
        LambdaExpression keySelector = Expression.Lambda(member, parameter);

        string methodName = (isFirst, descending) switch
        {
            (true, false) => nameof(Queryable.OrderBy),
            (true, true) => nameof(Queryable.OrderByDescending),
            (false, false) => nameof(Queryable.ThenBy),
            (false, true) => nameof(Queryable.ThenByDescending),
        };

        MethodCallExpression call = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(TEntity), clrType],
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<TEntity>(call);
    }

    private static IQueryable<TEntity> ApplyOrderBy<TEntity>(
        IQueryable<TEntity> source,
        PropertyInfo property,
        bool descending,
        bool isFirst)
        where TEntity : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        // A [QueryableValueObject] column sorts by its real `.Value` scalar column (ADR-070);
        // a plain converter-mapped VO still sorts whole-value (the converter round-trips).
        Expression member = ValueObjectMemberResolver.Resolve(parameter, property);
        LambdaExpression keySelector = Expression.Lambda(member, parameter);

        string methodName = (isFirst, descending) switch
        {
            (true, false) => nameof(Queryable.OrderBy),
            (true, true) => nameof(Queryable.OrderByDescending),
            (false, false) => nameof(Queryable.ThenBy),
            (false, true) => nameof(Queryable.ThenByDescending),
        };

        MethodCallExpression call = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(TEntity), member.Type],
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<TEntity>(call);
    }
}
