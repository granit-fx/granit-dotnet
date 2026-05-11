using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Providers;
using Granit.Imaging;

namespace Granit.Documents.Renditions.Imaging.Internal;

/// <summary>
/// <c>image/* → image/*</c> rendition provider. Loads the source via
/// <see cref="IImageProcessor"/>, optionally resizes to the target dimensions, encodes
/// to the requested MIME, and strips EXIF / IPTC / XMP metadata for GDPR compliance.
/// </summary>
internal sealed class ImagingRenditionProvider(IImageProcessor processor) : IRenditionProvider
{
    /// <inheritdoc />
    public string Name => "imaging";

    /// <inheritdoc />
    public string OutputContentType => "image/*";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType) =>
        !string.IsNullOrEmpty(sourceContentType) &&
        sourceContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<RenditionResult> GenerateAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        ImageFormat outputFormat = ResolveFormat(target.TargetContentType);

        await using IImagePipeline pipeline = processor.Load(source);

        pipeline.StripMetadata().ConvertTo(outputFormat);

        if (target.Dimensions is { } dims)
        {
            pipeline.Resize(dims.Width, dims.Height, ResizeMode.Max);
        }

        if (target.Quality is { } quality)
        {
            pipeline.Compress(quality);
        }

        ImageResult result = await pipeline.ToResultAsync(cancellationToken).ConfigureAwait(false);
        return new RenditionResult(
            result.Content.ToArray(),
            target.TargetContentType,
            result.Width,
            result.Height);
    }

    private static ImageFormat ResolveFormat(string targetContentType) => targetContentType?.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
        "image/png" => ImageFormat.Png,
        "image/webp" => ImageFormat.WebP,
        "image/avif" => ImageFormat.Avif,
        "image/gif" => ImageFormat.Gif,
        "image/bmp" => ImageFormat.Bmp,
        "image/tiff" => ImageFormat.Tiff,
        _ => throw new NotSupportedException(
            $"Unsupported target content type for imaging rendition: '{targetContentType}'."),
    };
}
