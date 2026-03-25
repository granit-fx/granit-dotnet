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
    public static IQueryable<TEntity> ApplySort<TEntity>(
        this IQueryable<TEntity> source,
        string? sort,
        QueryDefinitionBuilder<TEntity> builder)
        where TEntity : class
    {
        string effectiveSort = string.IsNullOrWhiteSpace(sort)
            ? builder.DefaultSortValue ?? string.Empty
            : sort;

        if (string.IsNullOrWhiteSpace(effectiveSort))
        {
            return source;
        }

        var sortableFields = builder.Columns
            .Where(c => c.IsSortable)
            .Select(c => c.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] parts = effectiveSort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool isFirst = true;

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

        return source;
    }

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
        MemberExpression member = Expression.Property(parameter, property);
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
            [typeof(TEntity), property.PropertyType],
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<TEntity>(call);
    }
}
