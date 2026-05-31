using Granit.DataExchange.Export;
using Granit.DataExchange.Xml.Internal.Export;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Xml.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DataExchange.Xml</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the XML export writer (format <c>"xml"</c>).
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IExportWriter"/> → <c>XmlExportWriter</c> (singleton) — export.</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataExchangeXml(this IServiceCollection services)
    {
        services.AddSingleton<IExportWriter, XmlExportWriter>();
        return services;
    }
}
