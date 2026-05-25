using Granit.Html.AngleSharp.Extensions;
using Granit.TextExtraction.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.TextExtraction.Text.Extensions;

/// <summary>
/// Extension methods for registering the HTML / Markdown text extractors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HtmlTextExtractor"/> and <see cref="MarkdownTextExtractor"/>
    /// with the <c>Granit.TextExtraction</c> pipeline. Implicitly calls
    /// <c>AddGranitTextExtraction()</c> and <c>AddGranitHtmlAngleSharp()</c> so consumers
    /// don't have to wire the base modules separately — <see cref="HtmlTextExtractor"/>
    /// needs the keyed <c>HtmlConverterKeys.Untrusted</c> converter.
    /// </summary>
    public static IServiceCollection AddGranitTextExtractionText(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddGranitHtmlAngleSharp();
        services.AddTextExtractor<HtmlTextExtractor>();
        services.AddTextExtractor<MarkdownTextExtractor>();

        return services;
    }
}
