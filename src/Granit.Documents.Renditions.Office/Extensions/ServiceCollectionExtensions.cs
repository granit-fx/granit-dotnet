using System;
using Granit.Documents.Renditions.Extensions;
using Granit.Documents.Renditions.Office.Internal;
using Granit.Documents.Renditions.Office.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.Renditions.Office.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.Renditions.Office</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the LibreOffice-headless–backed Office rendition provider plus its
    /// options binding. Hosts MUST install LibreOffice on the runtime image; absent
    /// binary surfaces as a descriptive runtime error on the first conversion attempt.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsRenditionsOffice(
        this IServiceCollection services,
        Action<OfficeRenditionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OfficeRenditionOptions>()
            .BindConfiguration(OfficeRenditionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.AddRenditionProvider<OfficeRenditionProvider>();
        return services;
    }
}
