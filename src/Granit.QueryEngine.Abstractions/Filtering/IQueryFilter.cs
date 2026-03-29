using System.Linq.Expressions;

namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Custom filter extension point for named filters that cannot be expressed
/// via simple property operators (e.g. computed or cross-entity filters).
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public interface IQueryFilter<TEntity> where TEntity : class
{
    /// <summary>
    /// Unique name of this filter (used in the query string as <c>filter[name.eq]=value</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the filter expression for the given value.
    /// </summary>
    /// <param name="value">The filter value from the query string.</param>
    /// <returns>A predicate expression to apply to the queryable.</returns>
    Expression<Func<TEntity, bool>> Apply(string value);
}
