using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Internal;
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
    /// <remarks>
    /// Idempotent for a given (<typeparamref name="TEntity"/>, <typeparamref name="TDefinition"/>)
    /// pair: registering the same definition more than once is a no-op, so modules sharing a base
    /// module do not register duplicate <see cref="IImportDefinitionDescriptor"/> or
    /// <see cref="IImportEntityBinding"/> entries.
    /// </remarks>
    public static IServiceCollection AddImportDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : ImportDefinition<TEntity>
    {
        if (services.Any(d => d.ServiceType == typeof(ImportDefinition<TEntity>)
            && d.ImplementationType == typeof(TDefinition)))
        {
            return services;
        }

        services.AddSingleton<ImportDefinition<TEntity>, TDefinition>();
        services.AddSingleton<IImportDefinitionDescriptor>(sp =>
            sp.GetRequiredService<ImportDefinition<TEntity>>());
        services.AddSingleton<IImportEntityBinding>(sp =>
            new ImportEntityBinding<TEntity>(sp.GetRequiredService<ImportDefinition<TEntity>>()));
        return services;
    }
}
