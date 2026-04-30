using Granit.QueryEngine.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.QueryEngine.Extensions;

/// <summary>
/// DI registration helpers for <see cref="QueryDefinition{TEntity}"/>.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.QueryEngine.Abstractions</c> so a base module can declare and
/// register its query definitions without taking a runtime dependency on
/// <c>Granit.QueryEngine</c> (engine, EF Core integration, AspNetCore endpoint mapper).
/// </remarks>
public static class QueryDefinitionServiceCollectionExtensions
{
    /// <summary>
    /// Registers a query definition for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <typeparam name="TDefinition">The query definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The concrete <typeparamref name="TDefinition"/> is registered as a resolvable
    /// singleton so callers (and the <c>Granit.Entities</c> integrity check) can locate
    /// it through DI — both the base <see cref="QueryDefinition{TEntity}"/> service and
    /// the non-generic <see cref="IQueryDefinitionDescriptor"/> resolve to the same
    /// instance.
    /// </remarks>
    public static IServiceCollection AddQueryDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : QueryDefinition<TEntity>, new()
    {
        services.AddSingleton<TDefinition>(sp =>
        {
            TDefinition definition = new();
            QueryEngineOptions options = sp.GetService<QueryEngineOptions>() ?? new();
            definition.Initialize(options);
            return definition;
        });
        services.AddSingleton<QueryDefinition<TEntity>>(sp => sp.GetRequiredService<TDefinition>());
        services.AddSingleton<IQueryDefinitionDescriptor>(sp => sp.GetRequiredService<TDefinition>());
        return services;
    }
}
