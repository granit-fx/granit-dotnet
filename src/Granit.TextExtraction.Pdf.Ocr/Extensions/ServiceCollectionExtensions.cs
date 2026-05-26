using Granit.TextExtraction.Extensions;
using Granit.TextExtraction.Pdf.Ocr.Internal;
using Granit.TextExtraction.Pdf.Ocr.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.TextExtraction.Pdf.Ocr.Extensions;

/// <summary>
/// Extension methods for the scanned-PDF OCR pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the base <see cref="Granit.TextExtraction.Pdf.PdfTextExtractor"/> with
    /// the OCR-aware <see cref="PdfOcrTextExtractor"/> and binds
    /// <see cref="PdfOcrOptions"/>. Call AFTER <c>AddGranitTextExtractionPdf()</c> and
    /// AFTER whichever image/png OCR extractor the host wires (Tesseract or AI) —
    /// the lookup happens at extraction time so registration order between
    /// <c>AddTesseractOcrExtractor</c> and this method is flexible, but BOTH must be
    /// registered before the pipeline runs.
    /// </summary>
    public static IServiceCollection AddGranitTextExtractionPdfOcr(
        this IServiceCollection services,
        Action<PdfOcrOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();

        services.AddOptions<PdfOcrOptions>()
            .BindConfiguration(PdfOcrOptions.SectionName);
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // CA1416 fires because DefaultPdfRasterizer carries the PDFium platform
        // constraints (Linux / Windows / macOS / iOS / Android / MacCatalyst). The set
        // covers every supported .NET runtime target, so propagating the constraints
        // onto every host-side AddGranitTextExtractionPdfOcr() call would add noise
        // without changing behaviour. Hosts that target a different platform will
        // surface the missing native lib at runtime anyway.
#pragma warning disable CA1416
        services.TryAddSingleton<IPdfRasterizer, DefaultPdfRasterizer>();
#pragma warning restore CA1416

        // Replace any previously-registered application/pdf extractor (typically
        // PdfTextExtractor from Granit.TextExtraction.Pdf) with the OCR-aware one.
        // Iterating in reverse so RemoveAt doesn't disturb the indices we still need.
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(ITextExtractor)
                && services[i].ImplementationType == typeof(Granit.TextExtraction.Pdf.PdfTextExtractor))
            {
                services.RemoveAt(i);
            }
        }

        services.AddTextExtractor<PdfOcrTextExtractor>();
        return services;
    }
}
