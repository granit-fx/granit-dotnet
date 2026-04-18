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
    public static IServiceCollection AddExportDefinition<TEntity, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TDefinition : ExportDefinition<TEntity>
    {
        services.AddSingleton<ExportDefinition<TEntity>, TDefinition>();
        services.AddSingleton<IExportDefinitionDescriptor>(sp =>
            sp.GetRequiredService<ExportDefinition<TEntity>>());
        return services;
    }
}
