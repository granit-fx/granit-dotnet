using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Specification;

/// <summary>
/// Applies a <see cref="Specification{T}"/> to an <see cref="IQueryable{T}"/> source.
/// EF Core named query filters (tenant, soft-delete, GDPR) remain active —
/// the specification cannot bypass them.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>Applies the specification's criteria, sorting, tracking, and pagination to the query.</summary>
    public static IQueryable<T> Apply<T>(
        IQueryable<T> source,
        Specification<T> spec) where T : class
    {
        IQueryable<T> query = source;

        foreach (System.Linq.Expressions.Expression<Func<T, bool>> criterion in spec.Criteria)
        {
            query = query.Where(criterion);
        }

        if (spec.IsReadOnly)
        {
            query = query.AsNoTracking();
        }

        bool first = true;
        foreach (SortExpression<T> sort in spec.OrderExpressions)
        {
            query = (first, sort.Ascending) switch
            {
                (true, true) => query.OrderBy(sort.KeySelector),
                (true, false) => query.OrderByDescending(sort.KeySelector),
                (false, true) => ((IOrderedQueryable<T>)query).ThenBy(sort.KeySelector),
                (false, false) => ((IOrderedQueryable<T>)query).ThenByDescending(sort.KeySelector),
            };
            first = false;
        }

        if (spec.Skip.HasValue)
        {
            query = query.Skip(spec.Skip.Value);
        }

        if (spec.Take.HasValue)
        {
            query = query.Take(spec.Take.Value);
        }

        return query;
    }

    /// <summary>Applies the specification with server-side projection.</summary>
    public static IQueryable<TResult> Apply<T, TResult>(
        IQueryable<T> source,
        Specification<T, TResult> spec) where T : class
    {
        IQueryable<T> query = Apply(source, (Specification<T>)spec);

        return spec.Selector is not null
            ? query.Select(spec.Selector)
            : throw new InvalidOperationException(
                $"Specification<{typeof(T).Name}, {typeof(TResult).Name}> requires a Selector. " +
                "Call Select() in the specification constructor.");
    }
}
