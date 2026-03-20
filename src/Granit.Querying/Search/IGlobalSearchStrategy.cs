namespace Granit.Querying.Search;

/// <summary>
/// Strategy for applying global free-text search to a queryable.
/// The default implementation uses <c>string.Contains()</c> (translates to <c>LIKE '%term%'</c>).
/// Replace with a provider-specific implementation (e.g. PostgreSQL FTS, trigram index)
/// for large tables where <c>LIKE '%term%'</c> causes full table scans.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IGlobalSearchStrategy<TEntity> where TEntity : class
{
    /// <summary>
    /// Applies a free-text search to the queryable using the declared search properties.
    /// </summary>
    /// <param name="source">The queryable to filter.</param>
    /// <param name="searchTerm">The user-supplied search term.</param>
    /// <param name="searchProperties">Property names declared via <c>GlobalSearch(...)</c>.</param>
    /// <returns>A filtered queryable.</returns>
    IQueryable<TEntity> ApplySearch(
        IQueryable<TEntity> source,
        string searchTerm,
        IReadOnlyList<string> searchProperties);
}
