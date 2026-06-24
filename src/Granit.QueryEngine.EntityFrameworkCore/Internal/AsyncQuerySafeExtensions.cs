using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Safe async wrappers that fall back to synchronous operations for non-EF Core queryables.
/// Required for in-memory <see cref="IQueryableSource{TEntity}"/> implementations (e.g.,
/// config-backed sources like <c>TaxRateEntryQueryableSource</c>) that return an
/// in-memory IQueryable and therefore don't implement <see cref="IAsyncQueryProvider"/>.
/// </summary>
internal static class AsyncQuerySafeExtensions
{
    internal static Task<int> CountSafeAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default) =>
        source.Provider is IAsyncQueryProvider
            ? source.CountAsync(cancellationToken)
            : Task.FromResult(source.Count());

    internal static Task<List<T>> ToListSafeAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default) =>
        source.Provider is IAsyncQueryProvider
            ? source.ToListAsync(cancellationToken)
            : Task.FromResult(source.ToList());

    internal static IAsyncEnumerable<T> AsAsyncEnumerableSafe<T>(this IQueryable<T> source) =>
        source.Provider is IAsyncQueryProvider
            ? source.AsAsyncEnumerable()
            : IterateSync(source);

    private static async IAsyncEnumerable<T> IterateSync<T>(
        IEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (T item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
