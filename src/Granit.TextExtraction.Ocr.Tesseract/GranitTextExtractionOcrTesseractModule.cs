using Granit.Imaging;
using Granit.Modularity;

namespace Granit.TextExtraction.Ocr.Tesseract;

/// <summary>
/// Granit module that registers the Tesseract OCR extractor (opt-in). Hosts wire the
/// extractor via
/// <see cref="Extensions.ServiceCollectionExtensions.AddTesseractOcrExtractor"/>;
/// the module itself does not auto-register anything because enabling OCR requires native
/// <c>libtesseract</c> plus traineddata files on disk — both of which are host-specific
/// deployment concerns. The extractor also resolves <see cref="IImageProcessor"/> for its
/// pre-decode pixel-bomb guard, so the host must register an imaging provider (e.g.
/// <c>Granit.Imaging.MagickNet</c>).
/// </summary>
[DependsOn(typeof(GranitImagingModule), typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionOcrTesseractModule : GranitModule;
