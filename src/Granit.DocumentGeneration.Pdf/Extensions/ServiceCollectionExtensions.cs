using Granit.Browsing;
using Granit.Diagnostics;
using Granit.DocumentGeneration.Pdf.Diagnostics;
using Granit.DocumentGeneration.Pdf.Internal;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DocumentGeneration.Pdf.Extensions;

/// <summary>Extension methods for registering <c>Granit.DocumentGeneration.Pdf</c> services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTML → PDF renderer (<see cref="IDocumentRenderer"/> →
    /// <see cref="BrowsingPdfRenderer"/>) on top of <c>Granit.Browsing</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts MUST register a <c>Granit.Browsing</c> provider that advertises
    /// <see cref="BrowserCapabilities.PdfGeneration"/> — typically
    /// <c>services.AddGranitBrowsingPuppeteerSharp()</c> or
    /// <c>services.AddGranitBrowsingPlaywright(opts =&gt; opts.Engine = BrowserEngine.Chromium)</c>
    /// — <b>before</b> calling this extension. A descriptive
    /// <see cref="InvalidOperationException"/> is thrown otherwise so the misconfiguration
    /// surfaces at boot rather than at the first PDF render.
    /// </para>
    /// <para>
    /// Configuration section: <c>DocumentGeneration:Pdf</c> →
    /// <see cref="PdfRenderOptions"/>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// When no <c>Granit.Browsing</c> provider has been registered before this call.
    /// </exception>
    public static IServiceCollection AddGranitDocumentGenerationPdf(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(d => d.ServiceType == typeof(IHeadlessBrowser)))
        {
            throw new InvalidOperationException(
                "Granit.DocumentGeneration.Pdf requires a Granit.Browsing provider with PDF capability. " +
                "Call services.AddGranitBrowsingPuppeteerSharp() or " +
                "services.AddGranitBrowsingPlaywright(opts => opts.Engine = BrowserEngine.Chromium) " +
                "before AddGranitDocumentGenerationPdf().");
        }

        services.AddOptions<PdfRenderOptions>()
            .BindConfiguration(PdfRenderOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDocumentRenderer, BrowsingPdfRenderer>();

        GranitActivitySourceRegistry.Register(PdfRenderingActivitySource.Name);
        return services;
    }
}
