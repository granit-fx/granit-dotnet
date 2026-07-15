using System.Diagnostics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.Exceptions;
using Granit.Imaging.MagickNet.Diagnostics;
using Granit.Imaging.MagickNet.Options;
using Granit.MultiTenancy;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.MagickNet.Internal;

/// <summary>
/// Magick.NET implementation of <see cref="IImageProcessor"/>.
/// Stateless singleton that creates <see cref="MagickNetImagePipeline"/> instances.
/// </summary>
/// <remarks>
/// <see cref="ICurrentTenant"/> is a singleton (AsyncLocal-backed), so injecting it here is
/// lifetime-safe; the pipeline reads the ambient tenant at terminal-operation time so the
/// metrics carry the tenant of the request that ran the pipeline.
/// </remarks>
internal sealed class MagickNetImageProcessor(
    ImagingMetrics metrics,
    ICurrentTenant currentTenant,
    IOptions<ImagingMagickNetOptions> options) : IImageProcessor
{
    /// <inheritdoc/>
    public async Task<IImagePipeline> LoadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using Activity? activity = ImagingMagickNetActivitySource.Source
            .StartActivity(ImagingMagickNetActivitySource.LoadOperation);

        if (!source.CanSeek)
        {
            MemoryStream buffer = new();
            await using (buffer.ConfigureAwait(false))
            {
                await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;

                ValidateInputSize(buffer.Length);
                ValidateFormat(buffer);

                MagickImage bufferedImage = new(buffer);
                return CreatePipeline(bufferedImage, buffer.Length, activity);
            }
        }

        ValidateInputSize(source.Length);
        await ValidateFormatAsync(source, cancellationToken).ConfigureAwait(false);

        MagickImage image = new();
        await image.ReadAsync(source, cancellationToken).ConfigureAwait(false);
        return CreatePipeline(image, source.Length, activity);
    }

    /// <inheritdoc/>
    public IImagePipeline Load(ReadOnlyMemory<byte> source)
    {
        using Activity? activity = ImagingMagickNetActivitySource.Source
            .StartActivity(ImagingMagickNetActivitySource.LoadOperation);

        ValidateInputSize(source.Length);

        if (!ImageFormatDetector.IsSafeRasterFormat(source.Span))
        {
            throw new UnsupportedImageFormatException("unknown");
        }

        MagickImage image = new(source.Span);
        return CreatePipeline(image, source.Length, activity);
    }

    /// <inheritdoc/>
    public ImageInfo Identify(ReadOnlyMemory<byte> source)
    {
        using Activity? activity = ImagingMagickNetActivitySource.Source
            .StartActivity(ImagingMagickNetActivitySource.IdentifyOperation);
        activity?.SetTag(ImagingMagickNetActivitySource.TagInputBytes, source.Length);

        // Header-only: deliberately NO size validation here. Identify never allocates a
        // decoded surface, so a large byte buffer is harmless — and callers rely on it
        // precisely to vet declared pixel dimensions BEFORE deciding whether to decode.
        if (!ImageFormatDetector.IsSafeRasterFormat(source.Span))
        {
            throw new UnsupportedImageFormatException("unknown");
        }

        try
        {
            MagickImageInfo info = new(source.Span);
            ImageInfo result = new(
                new ImageSize((int)info.Width, (int)info.Height),
                MagickFormatMapper.FromMagickFormat(info.Format));

            activity?.SetTag(ImagingMagickNetActivitySource.TagSourceFormat, result.Format.ToString());
            activity?.SetTag(ImagingMagickNetActivitySource.TagWidth, result.Size.Width);
            activity?.SetTag(ImagingMagickNetActivitySource.TagHeight, result.Size.Height);
            return result;
        }
        catch (MagickException ex)
        {
            throw new UnsupportedImageFormatException("unreadable", ex);
        }
    }

    private MagickNetImagePipeline CreatePipeline(MagickImage image, long inputBytes, Activity? activity)
    {
        MagickNetImagePipeline pipeline = new(image, metrics, currentTenant);

        activity?.SetTag(ImagingMagickNetActivitySource.TagInputBytes, inputBytes);
        activity?.SetTag(ImagingMagickNetActivitySource.TagSourceFormat, pipeline.SourceFormat.ToString());
        activity?.SetTag(ImagingMagickNetActivitySource.TagWidth, pipeline.SourceSize.Width);
        activity?.SetTag(ImagingMagickNetActivitySource.TagHeight, pipeline.SourceSize.Height);

        return pipeline;
    }

    private void ValidateInputSize(long length)
    {
        long maxInputBytes = options.Value.MaxInputBytes;
        if (maxInputBytes > 0 && length > maxInputBytes)
        {
            throw new InvalidOperationException(
                "Input image exceeds the maximum allowed size.");
        }
    }

    private static void ValidateFormat(Stream source)
    {
        // ReadAtLeast: a single Read may legally return fewer bytes than requested even
        // mid-stream, which would reject a valid image on a short first chunk.
        Span<byte> header = stackalloc byte[ImageFormatDetector.RequiredHeaderLength];
        long position = source.Position;
        int bytesRead = source.ReadAtLeast(header, ImageFormatDetector.RequiredHeaderLength, throwOnEndOfStream: false);
        source.Position = position;

        if (!ImageFormatDetector.IsSafeRasterFormat(header[..bytesRead]))
        {
            throw new UnsupportedImageFormatException("unknown");
        }
    }

    private static async Task ValidateFormatAsync(Stream source, CancellationToken cancellationToken)
    {
        byte[] header = new byte[ImageFormatDetector.RequiredHeaderLength];
        long position = source.Position;
        int bytesRead = await source
            .ReadAtLeastAsync(header, ImageFormatDetector.RequiredHeaderLength, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);
        source.Position = position;

        if (!ImageFormatDetector.IsSafeRasterFormat(header.AsSpan(0, bytesRead)))
        {
            throw new UnsupportedImageFormatException("unknown");
        }
    }
}
