using Granit.DataExchange.Export;
using Granit.DataExchange.Json.Internal.Export;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Json.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DataExchange.Json</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JSON export writer (format <c>"json"</c>).
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IExportWriter"/> → <c>JsonExportWriter</c> (singleton) — export.</item>
    /// </list>
    /// <para>
    /// Multiple <see cref="IExportWriter"/> implementations can coexist (CSV + Excel + JSON).
    /// The pipeline dispatches based on format name (export).
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataExchangeJson(this IServiceCollection services)
    {
        services.AddSingleton<IExportWriter, JsonExportWriter>();
        return services;
    }
}
