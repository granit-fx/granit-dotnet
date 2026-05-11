using System;
using Granit.Documents.Events;
using Granit.Documents.Renditions.Extensions;
using Granit.Documents.Renditions.Imaging.Internal;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Documents.Renditions.Imaging.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.Renditions.Imaging</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>image/* → image/*</c> rendition provider plus the optional
    /// synchronous-thumbnail hook that produces a small Ready thumbnail in-band when
    /// the upload is an image.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsRenditionsImaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRenditionProvider<ImagingRenditionProvider>();

        services.AddHttpClient(InlineThumbnailHandler.HttpClientName);
        services.AddScoped<ILocalEventHandler<DocumentVersionAddedEvent>, InlineThumbnailHandler>();

        return services;
    }
}
