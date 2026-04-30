using Microsoft.Extensions.DependencyInjection;

namespace Granit.Entities.Extensions;

/// <summary>
/// DI registration helpers for <see cref="EntityDefinition{TEntity}"/>.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.Entities.Abstractions</c> so a base module can declare and
/// register its entity definitions without taking a runtime dependency on
/// <c>Granit.Entities</c> (registry, integrity-check runner, manifest aggregator).
/// </remarks>
public static class EntityDefinitionServiceCollectionExtensions
{
    /// <summary>
    /// Registers an entity definition for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TDefinition">The entity definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEntityDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : EntityDefinition<TEntity>, new()
    {
        services.AddSingleton<EntityDefinition<TEntity>>(_ => new TDefinition());
        services.AddSingleton<IEntityDefinitionDescriptor>(sp =>
            sp.GetRequiredService<EntityDefinition<TEntity>>());
        return services;
    }
}
