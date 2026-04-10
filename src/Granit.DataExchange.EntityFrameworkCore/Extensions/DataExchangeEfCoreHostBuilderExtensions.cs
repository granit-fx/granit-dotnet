using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Pipeline;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.DataExchange.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the DataExchange EF Core persistence layer on <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class DataExchangeEfCoreHostBuilderExtensions
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

        return builder;
    }
}
