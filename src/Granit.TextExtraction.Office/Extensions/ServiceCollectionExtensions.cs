using Granit.TextExtraction.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.TextExtraction.Office.Extensions;

/// <summary>
/// Extension methods for registering the Office text extractors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WordTextExtractor"/>, <see cref="ExcelTextExtractor"/>, and
    /// <see cref="PowerPointTextExtractor"/> with the <c>Granit.TextExtraction</c> pipeline.
    /// Implicitly calls <c>AddGranitTextExtraction()</c> so consumers don't have to wire
    /// the base module separately.
    /// </summary>
    public static IServiceCollection AddGranitTextExtractionOffice(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<WordTextExtractor>();
        services.AddTextExtractor<ExcelTextExtractor>();
        services.AddTextExtractor<PowerPointTextExtractor>();

        return services;
    }
}
