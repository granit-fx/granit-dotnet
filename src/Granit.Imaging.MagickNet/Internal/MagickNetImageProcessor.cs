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
        Stream seekable = source;
        MemoryStream? buffer = null;

        try
        {
            if (!source.CanSeek)
            {
                buffer = new MemoryStream();
                source.CopyTo(buffer);
                buffer.Position = 0;
                seekable = buffer;
            }

            ValidateInputSize(seekable.Length);
            ValidateFormat(seekable);

            MagickImage image = new(seekable);
            return new MagickNetImagePipeline(image, metrics);
        }
        finally
        {
            buffer?.Dispose();
        }
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
