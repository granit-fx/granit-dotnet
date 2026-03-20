using System.Linq.Expressions;
using Granit.Querying.Search;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Default global search strategy using <c>string.Contains()</c>.
/// Translates to <c>LIKE '%term%'</c> in SQL — suitable for tables under ~50K rows.
/// For larger tables, use a provider-specific strategy (e.g. PostgreSQL FTS with trigram index).
/// </summary>
internal sealed class ContainsSearchStrategy<TEntity> : IGlobalSearchStrategy<TEntity>
    where TEntity : class
{
    /// <inheritdoc/>
    public IQueryable<TEntity> ApplySearch(
        IQueryable<TEntity> source,
        string searchTerm,
        IReadOnlyList<string> searchProperties)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchProperties.Count == 0)
        {
            return source;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? combined = null;

        foreach (string propertyName in searchProperties)
        {
            MemberExpression member = Expression.Property(parameter, propertyName);
            if (member.Type != typeof(string))
            {
                continue;
            }

            // e.Property != null && e.Property.Contains(searchTerm)
            Expression notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            Expression contains = Expression.Call(
                member,
                typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
                Expression.Constant(searchTerm));
            Expression predicate = Expression.AndAlso(notNull, contains);

            combined = combined is null ? predicate : Expression.OrElse(combined, predicate);
        }

        if (combined is null)
        {
            return source;
        }

        return source.Where(Expression.Lambda<Func<TEntity, bool>>(combined, parameter));
    }
}
