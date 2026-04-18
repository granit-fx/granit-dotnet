using Granit.DataExchange.Extensions;
using Granit.QueryEngine.Diagnostics;
using Granit.QueryEngine.Exports;
using Granit.QueryEngine.Options;
using Granit.QueryEngine.Queries;
using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.QueryEngine</c> runtime services.
/// </summary>
/// <remarks>
/// For declaring a query definition, see
/// <see cref="QueryDefinitionServiceCollectionExtensions.AddQueryDefinition{TEntity, TDefinition}"/>
/// in <c>Granit.QueryEngine.Abstractions</c>. Definitions are pure declarations and do
/// not require the runtime — only hosts that execute queries need
/// <c>AddGranitQueryEngine</c>.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core QueryEngine runtime infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="ISavedViewStoreReader"/> / <see cref="ISavedViewStoreWriter"/> (scoped) — null-object default.</item>
    ///   <item><see cref="QueryEngineOptions"/> singleton (resolved from <see cref="IOptions{T}"/>).</item>
    ///   <item><see cref="QueryEngineMetrics"/> singleton.</item>
    /// </list>
    /// <para>
    /// For the EF Core query engine, add <c>Granit.QueryEngine.EntityFrameworkCore</c>.
    /// For REST endpoints, add <c>Granit.QueryEngine.AspNetCore</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitQueryEngine(this IServiceCollection services)
    {
        // Default in-memory store — register concrete type, then forward both interfaces to same instance.
        services.TryAddScoped<NullSavedViewStore>();
        services.TryAddScoped<ISavedViewStoreReader>(sp => sp.GetRequiredService<NullSavedViewStore>());
        services.TryAddScoped<ISavedViewStoreWriter>(sp => sp.GetRequiredService<NullSavedViewStore>());

        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<QueryEngineOptions>>().Value);

        services.TryAddSingleton<QueryEngineMetrics>();

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<SavedView, SavedViewQueryDefinition>();
        services.AddExportDefinition<SavedView, SavedViewExportDefinition>();

        return services;
    }
}
