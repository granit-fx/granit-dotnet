using System;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Documents.Renditions.Extensions;
using Granit.Documents.Renditions.Pdf.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.Renditions.Pdf.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.Renditions.Pdf</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>application/pdf → image/png</c> rendition provider. Hosts MUST
    /// have wired a Chromium-backed <see cref="IHeadlessBrowser"/> beforehand
    /// (<c>AddGranitBrowsingPuppeteerSharp()</c> / <c>AddGranitBrowsingPlaywright()</c>)
    /// because the provider depends on <see cref="IPdfViewerCapability"/>.
    /// </summary>
    /// <remarks>
    /// A capability check runs at boot via <see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/>
    /// pattern — failures surface as a descriptive <see cref="InvalidOperationException"/>
    /// during host startup rather than at the first rendition attempt.
    /// </remarks>
    public static IServiceCollection AddGranitDocumentsRenditionsPdf(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRenditionProvider<PdfRenditionProvider>();

        services.AddSingleton<IPdfRenditionStartupValidator, PdfRenditionStartupValidator>();
        services.AddHostedService<PdfRenditionStartupHostedService>();

        return services;
    }
}
