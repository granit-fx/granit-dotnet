using Granit.DataExchange.Import;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Extensions;

/// <summary>
/// DI registration helpers for <see cref="ImportDefinition{TEntity}"/>.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.DataExchange.Abstractions</c> so a base module can declare and
/// register its import definitions without taking a runtime dependency on
/// <c>Granit.DataExchange</c> (orchestrator, parsers, jobs).
/// </remarks>
public static class ImportDefinitionServiceCollectionExtensions
{
    /// <summary>
    /// Registers an import definition for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <typeparam name="TDefinition">The import definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImportDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : ImportDefinition<TEntity>
    {
        services.AddSingleton<ImportDefinition<TEntity>, TDefinition>();
        services.AddSingleton<IImportDefinitionDescriptor>(sp =>
            sp.GetRequiredService<ImportDefinition<TEntity>>());
        return services;
    }
}
