using Granit.DataExchange.Export;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Extensions;

/// <summary>
/// DI registration helpers for <see cref="ExportDefinition{TEntity}"/>.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.DataExchange.Abstractions</c> so a base module can declare and
/// register its export definitions without taking a runtime dependency on
/// <c>Granit.DataExchange</c> (orchestrator, writers, jobs).
/// </remarks>
public static class ExportDefinitionServiceCollectionExtensions
{
    /// <summary>
    /// Registers an export definition for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The source entity type.</typeparam>
    /// <typeparam name="TDefinition">The export definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Idempotent for a given (<typeparamref name="TEntity"/>, <typeparamref name="TDefinition"/>)
    /// pair: registering the same definition more than once is a no-op. Modules sharing a base
    /// module (e.g. the Scriban and MJML templating engines both calling <c>AddGranitTemplating</c>)
    /// would otherwise register duplicate <see cref="IExportDefinitionDescriptor"/> entries.
    /// </remarks>
    public static IServiceCollection AddExportDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : ExportDefinition<TEntity>
    {
        if (services.Any(d => d.ServiceType == typeof(ExportDefinition<TEntity>)
            && d.ImplementationType == typeof(TDefinition)))
        {
            return services;
        }

        services.AddSingleton<ExportDefinition<TEntity>, TDefinition>();
        services.AddSingleton<IExportDefinitionDescriptor>(sp =>
            sp.GetRequiredService<ExportDefinition<TEntity>>());
        return services;
    }
}
