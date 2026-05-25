using Granit.TextExtraction.Extensions;
using Granit.TextExtraction.Ocr.Tesseract.Internal;
using Granit.TextExtraction.Ocr.Tesseract.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.TextExtraction.Ocr.Tesseract.Extensions;

/// <summary>
/// Extension methods for registering the Tesseract OCR extractor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TesseractOcrExtractor"/> with the <c>Granit.TextExtraction</c>
    /// pipeline. Implicitly calls <c>AddGranitTextExtraction()</c>, binds
    /// <see cref="TesseractOcrOptions"/>, and registers the default
    /// <see cref="ITesseractRecognizer"/>. The host MUST also ensure native
    /// <c>libtesseract</c> is on the loader path and that the configured
    /// <see cref="TesseractOcrOptions.DataPath"/> contains the requested
    /// <c>*.traineddata</c> files.
    /// </summary>
    public static IServiceCollection AddTesseractOcrExtractor(
        this IServiceCollection services,
        Action<TesseractOcrOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<TesseractOcrExtractor>();

        services.TryAddSingleton<ITesseractRecognizer, DefaultTesseractRecognizer>();

        services.AddOptions<TesseractOcrOptions>()
            .BindConfiguration(TesseractOcrOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
