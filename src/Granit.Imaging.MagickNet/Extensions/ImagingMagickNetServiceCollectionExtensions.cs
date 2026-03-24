using Granit.Diagnostics;
using Granit.Imaging.MagickNet.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Imaging.MagickNet.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Imaging.MagickNet</c> services.
/// </summary>
public static class ImagingMagickNetServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Magick.NET implementation of <see cref="IImageProcessor"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitImagingMagickNet(this IServiceCollection services)
    {
        GranitActivitySourceRegistry.Register(ImagingMagickNetActivitySource.Name);

        services.TryAddSingleton<ImagingMagickNetMetrics>();
        services.TryAddSingleton<IImageProcessor, MagickNetImageProcessor>();
        return services;
    }
}
