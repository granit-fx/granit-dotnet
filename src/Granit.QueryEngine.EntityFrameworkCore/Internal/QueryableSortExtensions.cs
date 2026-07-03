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
                bool applied;
                (source, applied) = TryApplySortPart(source, part, sortableFields, builder, isFirst);
                if (applied)
                {
                    isFirst = false;
                }
            }
        }

        // No explicit ordering resolved — guarantee a deterministic order so that
        // Skip/Take downstream is stable and EF Core does not warn.
        return isFirst ? ApplyFallbackOrder(source, builder) : source;
    }

    /// <summary>
    /// Applies a single <c>"field"</c> / <c>"-field"</c> sort token to <paramref name="source"/>,
    /// resolving it against the whitelisted shadow properties first and then the CLR type. Returns
    /// the (possibly reordered) queryable and whether an ordering was actually applied — an
    /// unrecognised or non-whitelisted token leaves the source untouched and reports
    /// <see langword="false"/>.
    /// </summary>
    private static (IQueryable<TEntity> Source, bool Applied) TryApplySortPart<TEntity>(
        IQueryable<TEntity> source,
        string part,
        HashSet<string> sortableFields,
        QueryDefinitionBuilder<TEntity> builder,
        bool isFirst)
        where TEntity : class
    {
        bool descending = part.StartsWith('-');
        string fieldName = descending ? part[1..] : part;

        if (!sortableFields.Contains(fieldName))
        {
            return (source, false);
        }

        // Check for shadow property first
        ColumnDescriptor? shadowCol = builder.Columns
            .FirstOrDefault(c => c.IsShadowProperty
                && string.Equals(c.PropertyName, fieldName, StringComparison.OrdinalIgnoreCase));

        if (shadowCol is not null)
        {
            return (ApplyOrderByShadow(source, fieldName, shadowCol.ClrType, descending, isFirst), true);
        }

        // Resolve the (possibly dotted) field to a member access, walking EF complex-type hops for a
        // nested column such as "Value.Street1". An unknown field leaves the source untouched.
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        (Expression? member, _) = MemberPathResolver.Resolve(parameter, fieldName);

        return member is null
            ? (source, false)
            : (ApplyOrderBy(source, parameter, member, descending, isFirst), true);
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
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");

        Expression? member = ResolveMember<TEntity>(parameter, builder.CursorPropertyName)
            ?? ResolveMember<TEntity>(parameter, "Id")
            ?? builder.Columns
                .Where(c => c.IsSortable && !c.IsShadowProperty)
                .Select(c => ResolveMember<TEntity>(parameter, c.PropertyName))
                .FirstOrDefault(m => m is not null);

        return member is null
            ? source
            : ApplyOrderBy(source, parameter, member, descending: false, isFirst: true);
    }

    private static Expression? ResolveMember<TEntity>(ParameterExpression parameter, string? name)
        where TEntity : class =>
        string.IsNullOrEmpty(name) ? null : MemberPathResolver.Resolve(parameter, name).Member;

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
        ParameterExpression parameter,
        Expression member,
        bool descending,
        bool isFirst)
        where TEntity : class
    {
        // `member` is already resolved by MemberPathResolver — a nested complex-member path is walked,
        // and a [QueryableValueObject] leaf sorts by its real `.Value` scalar column (ADR-070); a plain
        // converter-mapped VO still sorts whole-value (the converter round-trips).
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
