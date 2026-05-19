using Granit.DocumentGeneration.Excel.Extensions;
using Granit.Modularity;
using Granit.Templating;

namespace Granit.DocumentGeneration.Excel;

/// <summary>
/// Granit module that registers the ClosedXML Excel template engine.
/// </summary>
/// <remarks>
/// Registers:
/// <list type="bullet">
///   <item>
///     <c>ClosedXmlTemplateEngine</c> as <c>ITemplateEngine</c> (singleton).
///     Handles XLSX templates — returns <c>BinaryRenderedContent</c> directly,
///     no <c>IDocumentRenderer</c> required.
///   </item>
/// </list>
/// </remarks>
[DependsOn(typeof(GranitTemplatingModule))]
public sealed class GranitDocumentGenerationExcelModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDocumentGenerationExcel();
}
