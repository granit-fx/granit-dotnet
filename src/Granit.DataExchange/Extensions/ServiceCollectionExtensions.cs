using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Exports;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
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
    ///   <item><see cref="IDataExchangeFileProvider"/> (singleton) — fail-fast null-object default;
    ///     register <c>Granit.DataExchange.BlobStorage</c> or call
    ///     <see cref="AddInMemoryDataExchangeFileProvider"/>.</item>
    ///   <item><see cref="IImportOrchestrator"/> (scoped) — pipeline orchestrator.</item>
    /// </list>
    /// <para>
    /// At least one <see cref="Import.Parsing.IFileParser"/> must be registered separately.
    /// Use <c>Granit.DataExchange.Csv</c> or <c>Granit.DataExchange.Excel</c>.
    /// </para>
    /// <para>
    /// An <c>ICommandSender</c> implementation must be registered (<c>Granit.Wolverine</c>
    /// or another provider) — this module dispatches <see cref="Import.Messages.ExecuteImportCommand"/>
    /// via <c>ICommandSender</c> for asynchronous execution by <c>ExecuteImportCommandHandler</c>.
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
        services.TryAddSingleton<IDataExchangeFileProvider, NullDataExchangeFileProvider>();
        services.TryAddScoped<IImportOrchestrator, ImportOrchestrator>();
        services.TryAddScoped<IImportUploadService, ImportUploadService>();
        services.TryAddScoped<IImportPreviewService, ImportPreviewService>();

        // Diagnostics
        services.TryAddSingleton<DataExchangeMetrics>();
        GranitActivitySourceRegistry.Register(DataExchangeActivitySource.Name);

        // Event bus fallback (in-process default if not already registered)
        services.AddGranitEvents();

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<ImportJob, ImportJobQueryDefinition>();
        services.AddExportDefinition<ImportJob, ImportJobExportDefinition>();

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
    /// </list>
    /// <para>
    /// At least one <see cref="IExportWriter"/> must be registered separately.
    /// Use <c>Granit.DataExchange.Excel</c> or <c>Granit.DataExchange.Csv</c>.
    /// </para>
    /// <para>
    /// An <c>ICommandSender</c> implementation must be registered (<c>Granit.Wolverine</c>
    /// or another provider) — this module dispatches <see cref="Export.Messages.ExecuteExportCommand"/>
    /// via <c>ICommandSender</c> for asynchronous execution by <c>ExecuteExportCommandHandler</c>.
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

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<ExportJob, ExportJobQueryDefinition>();
        services.AddExportDefinition<ExportJob, ExportJobExportDefinition>();

        return services;
    }

    /// <summary>
    /// Replaces the default <see cref="IDataExchangeFileProvider"/> with the in-memory implementation.
    /// </summary>
    /// <remarks>
    /// Registered <b>Singleton</b> so uploaded files survive across DI scopes (upload request,
    /// preview request, message-handler execution). Files live in process memory and are lost on
    /// restart — suitable for tests and single-process CLI tools only. Production hosts register
    /// <c>Granit.DataExchange.BlobStorage</c> instead.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryDataExchangeFileProvider(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IDataExchangeFileProvider, InMemoryDataExchangeFileProvider>());
        return services;
    }

    /// <summary>
    /// Replaces the default <see cref="ISemanticMappingService"/> with an AI-backed implementation.
    /// </summary>
    /// <remarks>
    /// Registered <b>Scoped</b>: realistic implementations call the scoped
    /// <c>IStructuredCompletion</c> primitive (ADR-064), so a singleton here would capture a stale scope.
    /// </remarks>
    /// <typeparam name="TService">The semantic mapping service implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticMappingService<TService>(
        this IServiceCollection services)
        where TService : class, ISemanticMappingService
    {
        services.Replace(ServiceDescriptor.Scoped<ISemanticMappingService, TService>());
        return services;
    }
}
