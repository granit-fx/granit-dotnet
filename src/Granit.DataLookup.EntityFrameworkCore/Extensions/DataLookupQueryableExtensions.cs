using System.Linq.Expressions;
using Granit.DataLookup.EntityFrameworkCore.Sources;
using Granit.DataLookup.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataLookup.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering <see cref="QueryableLookupSource{T}"/> instances.
/// </summary>
public static class DataLookupQueryableExtensions
{
    /// <summary>
    /// Registers a scoped <see cref="QueryableLookupSource{T}"/> that sources its items
    /// from <c>dbContext.Set&lt;T&gt;()</c>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    public static IServiceCollection AddQueryableLookup<T, TDbContext>(
        this IServiceCollection services,
        string name,
        Expression<Func<T, object>> valueSelector,
        Expression<Func<T, string>> labelSelector,
        Expression<Func<T, string, bool>>? searchPredicate = null,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
        where T : class
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        services.AddScoped<ILookupSource>(sp =>
        {
            TDbContext db = sp.GetRequiredService<TDbContext>();
            return new QueryableLookupSource<T>(
                name,
                () => db.Set<T>().AsQueryable(),
                valueSelector,
                labelSelector,
                searchPredicate,
                requiredPermission,
                scopeKeys);
        });

        return services;
    }
}
