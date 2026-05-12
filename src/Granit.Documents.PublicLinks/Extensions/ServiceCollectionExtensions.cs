using Granit.Documents.PublicLinks.Diagnostics;
using Granit.Documents.PublicLinks.Internal;
using Granit.Documents.PublicLinks.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.PublicLinks.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.PublicLinks</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the public-links options + diagnostics. The storage companion
    /// (<c>Granit.Documents.PublicLinks.EntityFrameworkCore</c>) binds the
    /// <see cref="IDocumentPublicLinkService"/> implementation; the endpoints
    /// package mounts the HTTP surface.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsPublicLinks(
        this IServiceCollection services,
        Action<GranitDocumentsPublicLinksOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GranitDocumentsPublicLinksOptions>()
            .BindConfiguration(GranitDocumentsPublicLinksOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton<DocumentsPublicLinksMetrics>();
        services.TryAddScoped<IDocumentPublicLinkService, DocumentPublicLinkService>();
        return services;
    }
}
