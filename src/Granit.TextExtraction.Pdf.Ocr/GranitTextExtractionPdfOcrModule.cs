using Granit.Modularity;

namespace Granit.TextExtraction.Pdf.Ocr;

/// <summary>
/// Granit module for the scanned-PDF OCR pipeline. The module class is registration-only —
/// the actual extractor opt-in lives on
/// <see cref="Extensions.ServiceCollectionExtensions.AddGranitTextExtractionPdfOcr"/>
/// because enabling OCR requires both the native PDFium binary (from
/// <c>PDFtoImage</c>) and an <c>image/png</c> <see cref="ITextExtractor"/> wired by
/// the host (Tesseract or AI). Auto-registering would silently break hosts that
/// have neither dep.
/// </summary>
[DependsOn(
    typeof(GranitTextExtractionModule),
    typeof(Granit.TextExtraction.Pdf.GranitTextExtractionPdfModule))]
public sealed class GranitTextExtractionPdfOcrModule : GranitModule;
