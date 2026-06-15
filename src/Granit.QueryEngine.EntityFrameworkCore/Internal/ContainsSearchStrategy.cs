using System.Linq.Expressions;
using Granit.QueryEngine.Search;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

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

        string sanitized = LikeWildcardEscaper.Escape(searchTerm);

        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? combined = null;

        foreach (string propertyName in searchProperties)
        {
            // A [QueryableValueObject] column is drilled to its `.Value` string column so LIKE
            // translates (ADR-070, strategy B). A plain value-object column stays the VO type and
            // is skipped below (substring is unsupported on a value-converted column).
            System.Reflection.PropertyInfo? property = typeof(TEntity).GetProperty(propertyName);
            Expression member = property is not null
                ? ValueObjectMemberResolver.Resolve(parameter, property)
                : Expression.Property(parameter, propertyName);

            if (member.Type != typeof(string))
            {
                continue;
            }

            // e.Property != null && e.Property.Contains(sanitizedTerm)
            Expression notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
            Expression contains = Expression.Call(
                member,
                typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
                Expression.Constant(sanitized));
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

/// <summary>
/// Shared helpers for LIKE wildcard escaping.
/// </summary>
internal static class LikeWildcardEscaper
{
    /// <summary>
    /// Escapes SQL LIKE wildcard characters (<c>%</c>, <c>_</c>, <c>[</c>) to prevent
    /// wildcard injection in <c>string.Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>
    /// calls translated to LIKE by EF Core (CWE-943).
    /// </summary>
    internal static string Escape(string value) =>
        value.Replace("\\", "\\\\")
             .Replace("%", "\\%")
             .Replace("_", "\\_")
             .Replace("[", "\\[");
}
