using Granit.DataExchange.Csv.Internal.Export;
using Granit.DataExchange.Csv.Internal.Import;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DataExchange.Csv.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DataExchange.Csv</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sep-based CSV file parser (import), the CSV export writer, and the
    /// CSV correction file generator.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IFileParser"/> → <c>SepCsvFileParser</c> (singleton) — import.</item>
    ///   <item><see cref="IExportWriter"/> → <c>CsvExportWriter</c> (singleton) — export.</item>
    ///   <item><see cref="ICorrectionFileGenerator"/> → <c>CsvCorrectionFileGenerator</c> (singleton) — import correction file.</item>
    /// </list>
    /// <para>
    /// Multiple <see cref="IFileParser"/> and <see cref="IExportWriter"/> implementations
    /// can coexist (CSV + Excel). The pipeline dispatches based on MIME type (import)
    /// or format name (export). <see cref="ICorrectionFileGenerator"/> is a single-impl seam
    /// per host — registered with <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
    /// so a host wiring multiple <c>*.DataExchange.*</c> format packages keeps whichever is registered first.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataExchangeCsv(this IServiceCollection services)
    {
        services.AddSingleton<IFileParser, SepCsvFileParser>();
        services.AddSingleton<IExportWriter, CsvExportWriter>();
        services.TryAddSingleton<ICorrectionFileGenerator, CsvCorrectionFileGenerator>();
        return services;
    }
}
