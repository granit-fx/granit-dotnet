using Granit.Html.AngleSharp.Extensions;
using Granit.TextExtraction.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.TextExtraction.Email.Extensions;

/// <summary>
/// Extension methods for registering the email (.eml) text extractor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EmailTextExtractor"/> with the <c>Granit.TextExtraction</c>
    /// pipeline. Implicitly calls <c>AddGranitTextExtraction()</c> and
    /// <c>AddGranitHtmlAngleSharp()</c> so consumers don't have to wire the base modules
    /// separately — <see cref="EmailTextExtractor"/> needs the keyed
    /// <c>HtmlConverterKeys.Untrusted</c> converter for the HTML body fallback path.
    /// </summary>
    public static IServiceCollection AddGranitTextExtractionEmail(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddGranitHtmlAngleSharp();
        services.AddTextExtractor<EmailTextExtractor>();

        return services;
    }
}
