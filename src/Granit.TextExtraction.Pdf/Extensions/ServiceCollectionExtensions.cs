using Granit.TextExtraction.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.TextExtraction.Pdf.Extensions;

/// <summary>
/// Extension methods for registering the PDF text extractor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PdfTextExtractor"/> with the <c>Granit.TextExtraction</c>
    /// pipeline. Implicitly calls <c>AddGranitTextExtraction()</c> so consumers don't have
    /// to wire the base module separately.
    /// </summary>
    public static IServiceCollection AddGranitTextExtractionPdf(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<PdfTextExtractor>();

        return services;
    }
}
