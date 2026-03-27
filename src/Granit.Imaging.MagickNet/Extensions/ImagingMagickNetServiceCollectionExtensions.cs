using Granit.Diagnostics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Imaging.MagickNet.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Imaging.MagickNet</c> services.
/// </summary>
public static class ImagingMagickNetServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Magick.NET implementation of <see cref="IImageProcessor"/>
    /// with secure defaults for resource limits and format allowlisting.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to customize security and resource options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitImagingMagickNet(
        this IServiceCollection services,
        Action<ImagingMagickNetOptions>? configure = null)
    {
        GranitActivitySourceRegistry.Register(ImagingMagickNetActivitySource.Name);

        var options = new ImagingMagickNetOptions();
        configure?.Invoke(options);

        ApplyResourceLimits(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<ImagingMetrics>();
        services.TryAddSingleton<IImageProcessor, MagickNetImageProcessor>();
        return services;
    }

    private static void ApplyResourceLimits(ImagingMagickNetOptions options)
    {
        if (options.MaxMemoryBytes > 0)
        {
            ResourceLimits.Memory = (ulong)options.MaxMemoryBytes;
        }

        if (options.MaxWidthPixels > 0)
        {
            ResourceLimits.Width = (ulong)options.MaxWidthPixels;
        }

        if (options.MaxHeightPixels > 0)
        {
            ResourceLimits.Height = (ulong)options.MaxHeightPixels;
        }

        if (options.MaxListLength > 0)
        {
            ResourceLimits.ListLength = (ulong)options.MaxListLength;
        }
    }
}
