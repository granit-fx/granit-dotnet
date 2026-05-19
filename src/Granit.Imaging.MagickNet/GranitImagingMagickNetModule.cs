using Granit.Imaging.MagickNet.Extensions;
using Granit.Modularity;

namespace Granit.Imaging.MagickNet;

/// <summary>
/// Granit module for image processing via Magick.NET.
/// </summary>
/// <remarks>
/// Registers <see cref="IImageProcessor"/> as a singleton using the Magick.NET engine.
/// <para>
/// Supported formats: JPEG, PNG, WebP, AVIF, GIF, BMP, TIFF.
/// </para>
/// <para>
/// License: Magick.NET is Apache 2.0 — free for all use, including proprietary.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitImagingModule))]
public sealed class GranitImagingMagickNetModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitImagingMagickNet();
}
