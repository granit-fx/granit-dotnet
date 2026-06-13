using Granit.DocumentGeneration.Excel.Internal;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DocumentGeneration.Excel.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.DocumentGeneration.Excel</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ClosedXML Excel template engine.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="ITemplateEngine"/> → <c>ClosedXmlTemplateEngine</c> (singleton).
    ///     Handles templates with MIME type
    ///     <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c>.
    ///   </item>
    /// </list>
    /// <para>
    /// Must be called after <c>AddGranitTemplating()</c>.
    /// The template content stored in <see cref="Granit.Templating.Pipeline.TemplateDescriptor"/>
    /// must be a Base64-encoded XLSX workbook.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDocumentGenerationExcel(
        this IServiceCollection services)
    {
        // TryAddEnumerable: ITemplateEngine is a set (selected per-template via CanRender).
        // Coexists with Scriban and any other engine, and dedupes on repeat registration.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITemplateEngine, ClosedXmlTemplateEngine>());
        return services;
    }
}
