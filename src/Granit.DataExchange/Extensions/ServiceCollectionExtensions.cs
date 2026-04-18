using System.Threading.Channels;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Export.Messages;
using Granit.DataExchange.Exports;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Internal;
using Granit.DataExchange.Queries;
using Granit.Diagnostics;
using Granit.Events.Extensions;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DataExchange.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DataExchange</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core data import pipeline infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IMappingSuggestionService"/> (scoped) — 4-tier mapping facade.</item>
    ///   <item><see cref="ISemanticMappingService"/> (singleton) — null-object default.</item>
    ///   <item><see cref="IImportJobReader"/> / <see cref="IImportJobWriter"/> (scoped) — null-object default.</item>
    ///   <item><see cref="IDataExchangeFileProvider"/> (scoped) — in-memory default.</item>
    ///   <item><see cref="IImportOrchestrator"/> (scoped) — pipeline orchestrator.</item>
    ///   <item><see cref="IImportCommandDispatcher"/> (singleton) — channel-based dispatch.</item>
    /// </list>
    /// <para>
    /// At least one <see cref="Parsing.IFileParser"/> must be registered separately.
    /// Use <c>Granit.DataExchange.Csv</c> or <c>Granit.DataExchange.Excel</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataImport(this IServiceCollection services)
    {
        services.AddOptions<ImportOptions>()
            .BindConfiguration(ImportOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<ISemanticMappingService, NullSemanticMappingService>();
        services.TryAddScoped<IMappingSuggestionService, MappingSuggestionService>();
        services.TryAddScoped<IImportJobReader, NullImportJobStore>();
        services.TryAddScoped<IImportJobWriter, NullImportJobStore>();
        services.TryAddScoped<IDataExchangeFileProvider, InMemoryDataExchangeFileProvider>();
        services.TryAddScoped<IImportOrchestrator, ImportOrchestrator>();
        services.TryAddScoped<IImportUploadService, ImportUploadService>();
        services.TryAddScoped<IImportPreviewService, ImportPreviewService>();

        // Diagnostics
        services.TryAddSingleton<DataExchangeMetrics>();
        GranitActivitySourceRegistry.Register(DataExchangeActivitySource.Name);

        // Event bus fallback (in-process default if not already registered)
        services.AddGranitEvents();

        // Channel-based async dispatch (default). Replaced by Wolverine if installed.
        services.TryAddSingleton(Channel.CreateBounded<ExecuteImportCommand>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }));
        services.TryAddSingleton<IImportCommandDispatcher, ChannelImportCommandDispatcher>();
        services.AddHostedService<ImportCommandWorker>();

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<ImportJob, ImportJobQueryDefinition>();
        services.AddExportDefinition<ImportJob, ImportJobExportDefinition>();

        return services;
    }

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

    /// <summary>
    /// Registers the core data export pipeline infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IExportOrchestrator"/> (scoped) — export pipeline orchestrator.</item>
    ///   <item><see cref="IExportJobReader"/> / <see cref="IExportJobWriter"/> (scoped) — null-object default.</item>
    ///   <item><see cref="IExportPresetReader"/> / <see cref="IExportPresetWriter"/> (scoped) — null-object default.</item>
    ///   <item><see cref="IExportCommandDispatcher"/> (singleton) — channel-based dispatch.</item>
    /// </list>
    /// <para>
    /// At least one <see cref="IExportWriter"/> must be registered separately.
    /// Use <c>Granit.DataExchange.Excel</c> or <c>Granit.DataExchange.Csv</c>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataExport(this IServiceCollection services)
    {
        services.AddOptions<ExportOptions>()
            .BindConfiguration(ExportOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddScoped<IExportOrchestrator, ExportOrchestrator>();
        services.TryAddScoped<IExportDefinitionProvider, ExportDefinitionProvider>();
        services.TryAddScoped<IExportJobReader, NullExportJobStore>();
        services.TryAddScoped<IExportJobWriter, NullExportJobStore>();
        services.TryAddScoped<IExportPresetReader, NullExportPresetStore>();
        services.TryAddScoped<IExportPresetWriter, NullExportPresetStore>();

        // Extra property support: defaults replaced by EF Core layer
        services.TryAddSingleton<IExtraExportFieldProvider, NullExtraExportFieldProvider>();
        services.TryAddScoped<IExportExtraValueResolver, NullExportExtraValueResolver>();

        // Diagnostics
        services.TryAddSingleton<DataExchangeMetrics>();
        GranitActivitySourceRegistry.Register(DataExchangeActivitySource.Name);

        // Event bus fallback (in-process default if not already registered)
        services.AddGranitEvents();

        // Channel-based async dispatch (default). Replaced by Wolverine if installed.
        services.TryAddSingleton(Channel.CreateBounded<ExecuteExportCommand>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }));
        services.TryAddSingleton<IExportCommandDispatcher, ChannelExportCommandDispatcher>();
        services.AddHostedService<ExportCommandWorker>();

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<ExportJob, ExportJobQueryDefinition>();
        services.AddExportDefinition<ExportJob, ExportJobExportDefinition>();

        return services;
    }

    /// <summary>
    /// Replaces the default <see cref="ISemanticMappingService"/> with an AI-backed implementation.
    /// </summary>
    /// <typeparam name="TService">The semantic mapping service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticMappingService<TService>(
        this IServiceCollection services)
        where TService : class, ISemanticMappingService
    {
        services.Replace(ServiceDescriptor.Singleton<ISemanticMappingService, TService>());
        return services;
    }
}
