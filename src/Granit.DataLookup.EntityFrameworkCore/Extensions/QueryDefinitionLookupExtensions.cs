using Granit.DataLookup.EntityFrameworkCore.Sources;
using Granit.DataLookup.Sources;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataLookup.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering <see cref="QueryDefinitionLookupSource{TEntity}"/> instances.
/// </summary>
public static class QueryDefinitionLookupExtensions
{
    /// <summary>
    /// Registers a scoped <see cref="QueryDefinitionLookupSource{TEntity}"/> that reuses a
    /// <see cref="QueryDefinition{TEntity}"/> — which MUST declare <c>AsLookup</c> — and the
    /// open-generic <see cref="IQueryEngine{TEntity}"/> to serve typeahead lookups, including
    /// keyset/cursor pagination.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TDbContext">The DbContext exposing <c>Set&lt;TEntity&gt;()</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The host must already have registered the query definition
    /// (<c>AddQueryDefinition&lt;TEntity, TDefinition&gt;</c>) and the query engine runtime
    /// (<c>Granit.QueryEngine.EntityFrameworkCore</c>); both are resolved per request.
    /// </remarks>
    public static IServiceCollection AddQueryDefinitionLookup<TEntity, TDbContext>(
        this IServiceCollection services)
        where TEntity : class
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ILookupSource>(sp =>
        {
            IQueryEngine<TEntity> engine = sp.GetRequiredService<IQueryEngine<TEntity>>();
            QueryDefinition<TEntity> definition = sp.GetRequiredService<QueryDefinition<TEntity>>();
            TDbContext db = sp.GetRequiredService<TDbContext>();
            return new QueryDefinitionLookupSource<TEntity>(engine, definition, () => db.Set<TEntity>());
        });

        return services;
    }
}
