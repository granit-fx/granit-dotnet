using Granit.DocumentGeneration;
using Granit.DocumentGeneration.Excel;
using Granit.DocumentGeneration.Pdf;
using Granit.Modularity;
using Granit.Templating;
using Granit.Templating.EntityFrameworkCore;
using Granit.Templating.Scriban;

namespace Granit.Bundle.Documents;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the Documents bundle.
/// </summary>
public static class GranitBuilderDocumentsExtensions
{
    /// <summary>
    /// Adds the Documents bundle: Templating (Scriban), EntityFrameworkCore store,
    /// DocumentGeneration (PDF, Excel).
    /// </summary>
    public static GranitBuilder AddDocuments(this GranitBuilder builder)
    {
        builder.AddModule<GranitTemplatingModule>();
        builder.AddModule<GranitTemplatingScribanModule>();
        builder.AddModule<GranitTemplatingEntityFrameworkCoreModule>();
        builder.AddModule<GranitDocumentGenerationModule>();
        builder.AddModule<GranitDocumentGenerationPdfModule>();
        builder.AddModule<GranitDocumentGenerationExcelModule>();
        return builder;
    }
}
