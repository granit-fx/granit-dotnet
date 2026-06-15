using Granit.Imaging.Diagnostics;
using Granit.Imaging.Exceptions;
using Granit.Imaging.MagickNet.Options;
using ImageMagick;

namespace Granit.Imaging.MagickNet.Internal;

/// <summary>
/// Magick.NET implementation of <see cref="IImageProcessor"/>.
/// Stateless singleton that creates <see cref="MagickNetImagePipeline"/> instances.
/// </summary>
internal sealed class MagickNetImageProcessor(ImagingMetrics metrics, ImagingMagickNetOptions options) : IImageProcessor
{
    /// <inheritdoc/>
    public IImagePipeline Load(Stream source)
    {
        if (!source.CanSeek)
        {
            using MemoryStream buffer = new();
            source.CopyTo(buffer);
            buffer.Position = 0;

            ValidateInputSize(buffer.Length);
            ValidateFormat(buffer);

            MagickImage bufferedImage = new(buffer);
            return new MagickNetImagePipeline(bufferedImage, metrics);
        }

        ValidateInputSize(source.Length);
        ValidateFormat(source);

        MagickImage image = new(source);
        return new MagickNetImagePipeline(image, metrics);
    }

    /// <inheritdoc/>
    public IImagePipeline Load(ReadOnlyMemory<byte> source)
    {
        ValidateInputSize(source.Length);

        if (!ImageFormatDetector.IsSafeRasterFormat(source.Span))
        {
            throw new UnsupportedImageFormatException("unknown");
        }

        MagickImage image = new(source.Span);
        return new MagickNetImagePipeline(image, metrics);
    }

    /// <inheritdoc/>
    public ImageInfo Identify(ReadOnlyMemory<byte> source)
    {
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
            return new ImageInfo(
                new ImageSize((int)info.Width, (int)info.Height),
                MagickFormatMapper.FromMagickFormat(info.Format));
        }
        catch (MagickException ex)
        {
            throw new UnsupportedImageFormatException("unreadable", ex);
        }
    }

    private void ValidateInputSize(long length)
    {
        if (options.MaxInputBytes > 0 && length > options.MaxInputBytes)
        {
            throw new InvalidOperationException(
                "Input image exceeds the maximum allowed size.");
        }
    }

    private static void ValidateFormat(Stream source)
    {
        Span<byte> header = stackalloc byte[ImageFormatDetector.RequiredHeaderLength];
        long position = source.Position;
        int bytesRead = source.Read(header);
        source.Position = position;

        if (!ImageFormatDetector.IsSafeRasterFormat(header[..bytesRead]))
        {
            throw new UnsupportedImageFormatException("unknown");
        }
    }
}
