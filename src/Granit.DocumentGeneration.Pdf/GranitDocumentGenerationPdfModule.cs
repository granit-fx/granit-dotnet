using Granit.DocumentGeneration.Pdf.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DocumentGeneration.Pdf;

/// <summary>
/// Granit module that registers the PuppeteerSharp PDF renderer.
/// </summary>
/// <remarks>
/// Registers:
/// <list type="bullet">
///   <item>
///     <c>PuppeteerSharpRenderer</c> as <c>IDocumentRenderer</c> (singleton).
///     Converts rendered HTML into PDF using headless Chromium.
///   </item>
///   <item>
///     <c>ChromiumLifetimeService</c> as <c>IHostedService</c> (singleton).
///     Manages the Chromium browser lifecycle.
///   </item>
/// </list>
/// </remarks>
[DependsOn(typeof(GranitDocumentGenerationModule))]
public sealed class GranitDocumentGenerationPdfModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDocumentGenerationPdf();
}
