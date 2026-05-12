using System;
using Granit.Documents.Renditions.Wolverine.Internal;
using Granit.Documents.Renditions.Wolverine.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Documents.Renditions.Wolverine.Extensions;

/// <summary>DI extensions for <c>Granit.Documents.Renditions.Wolverine</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the F16.4 event-driven generation flow:
    /// <see cref="DefaultRenditionTypePolicy"/>, the orchestration service, and the
    /// HTTP-based source fetcher + result uploader. Wolverine discovers
    /// <see cref="Handlers.DocumentVersionAddedRenditionsHandler"/> from the assembly.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsRenditionsWolverine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(HttpRenditionSourceFetcher.HttpClientName);
        services.AddHttpClient(HttpRenditionResultUploader.HttpClientName);

        services.TryAddSingleton<IRenditionTypePolicy, DefaultRenditionTypePolicy>();
        services.TryAddSingleton<IRenditionSourceFetcher, HttpRenditionSourceFetcher>();
        services.TryAddSingleton<IRenditionResultUploader, HttpRenditionResultUploader>();
        services.TryAddSingleton<IRenditionGenerationService, RenditionGenerationService>();

        return services;
    }
}
