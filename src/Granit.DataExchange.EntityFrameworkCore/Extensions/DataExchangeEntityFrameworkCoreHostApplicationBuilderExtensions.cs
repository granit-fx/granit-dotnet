using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Export;
using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.DataExchange.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the DataExchange EF Core persistence layer on <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class DataExchangeEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the DataExchange EF Core persistence layer, including the isolated
    /// <c>DataExchangeDbContext</c>, mapping store, import job store,
    /// and export stores (job + presets).
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure the database provider (e.g. <c>opts.UseNpgsql(cs)</c>).</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDataExchangeEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<DataExchangeDbContext>(configure);

        // Import stores
        builder.Services.AddScoped<IMappingReader, EfMappingStore>();
        builder.Services.AddScoped<IMappingWriter, EfMappingStore>();
        builder.Services.AddScoped<IImportJobReader, EfImportJobStore>();
        builder.Services.AddScoped<IImportJobWriter, EfImportJobStore>();

        // Export stores (replace null-object defaults from Granit.DataExchange)
        builder.Services.AddScoped<IExportJobReader, EfExportJobStore>();
        builder.Services.AddScoped<IExportJobWriter, EfExportJobStore>();
        builder.Services.AddScoped<IExportPresetReader, EfExportPresetStore>();
        builder.Services.AddScoped<IExportPresetWriter, EfExportPresetStore>();

        // Auto-export fallback: generic data source + entity type discovery
        builder.Services.TryAddScoped(typeof(IExportDataSource<>), typeof(DbContextExportDataSource<>));
        builder.Services.TryAddSingleton<IAutoExportDefinitionSource, DbContextAutoExportDefinitionSource>();

        // Extra property support: replaces null-object defaults from Granit.DataExchange
        builder.Services.AddSingleton<IExtraExportFieldProvider, EfCoreExtraExportFieldProvider>();
        builder.Services.AddScoped<IExportExtraValueResolver, EfCoreExportExtraValueResolver>();

        // Query engine sources — back MapGranitQuery<T> + the analytics runner over
        // ImportJobQuery / ExportJobQuery.
        builder.Services.AddScoped<IQueryableSource<ImportJob>, EfImportJobQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<ExportJob>, EfExportJobQueryableSource>();

        return builder;
    }
}
