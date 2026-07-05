using System.Diagnostics;
using Granit.Imaging.Diagnostics;
using ImageMagick;

namespace Granit.Imaging.MagickNet.Internal;

/// <summary>
/// Magick.NET implementation of <see cref="IImagePipeline"/>.
/// Each transformation mutates the underlying <see cref="MagickImage"/> immediately (eager execution).
/// </summary>
internal sealed class MagickNetImagePipeline : IImagePipeline
{
    private readonly MagickImage _image;
    private readonly ImagingMetrics _metrics;
    private readonly long _startTimestamp;
    private int? _quality;
    private ImageFormat? _targetFormat;

    internal MagickNetImagePipeline(MagickImage image, ImagingMetrics metrics)
    {
        _image = image;
        _metrics = metrics;
        _startTimestamp = Stopwatch.GetTimestamp();
        SourceSize = new ImageSize((int)_image.Width, (int)_image.Height);
        SourceFormat = MagickFormatMapper.FromMagickFormat(_image.Format);
    }

    /// <inheritdoc/>
    public ImageSize SourceSize { get; }

    /// <inheritdoc/>
    public ImageFormat SourceFormat { get; }

    /// <inheritdoc/>
    public IImagePipeline Resize(int width, int height, ResizeMode mode = ResizeMode.Max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        MagickGeometry geometry = new((uint)width, (uint)height);

        switch (mode)
        {
            case ResizeMode.Max:
                geometry.IgnoreAspectRatio = false;
                _image.Resize(geometry);
                break;

            case ResizeMode.Crop:
                geometry.IgnoreAspectRatio = false;
                geometry.FillArea = true;
                _image.Resize(geometry);
                _image.Crop((uint)width, (uint)height, Gravity.Center);
                _image.ResetPage();
                break;

            case ResizeMode.Pad:
                geometry.IgnoreAspectRatio = false;
                _image.Resize(geometry);
                _image.Extent((uint)width, (uint)height, Gravity.Center, MagickColors.Transparent);
                break;

            case ResizeMode.Stretch:
                geometry.IgnoreAspectRatio = true;
                _image.Resize(geometry);
                break;

            case ResizeMode.Min:
                geometry.IgnoreAspectRatio = false;
                geometry.FillArea = true;
                _image.Resize(geometry);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown resize mode.");
        }

        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline Crop(CropRectangle rectangle)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rectangle.X);
        ArgumentOutOfRangeException.ThrowIfNegative(rectangle.Y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rectangle.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rectangle.Height);

        _image.Crop(new MagickGeometry(rectangle.X, rectangle.Y, (uint)rectangle.Width, (uint)rectangle.Height));
        _image.ResetPage();
        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline Compress(int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        _quality = quality;
        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline ConvertTo(ImageFormat format)
    {
        _targetFormat = format;
        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline Watermark(
        ReadOnlyMemory<byte> watermark,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = 0.5f)
    {
        using MagickImage overlay = new(watermark.Span);
        ApplyWatermark(overlay, position, opacity);
        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline Watermark(
        Stream watermark,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = 0.5f)
    {
        using MagickImage overlay = new(watermark);
        ApplyWatermark(overlay, position, opacity);
        return this;
    }

    /// <inheritdoc/>
    public IImagePipeline StripMetadata()
    {
        _image.Strip();
        return this;
    }

    /// <inheritdoc/>
    public async Task<ImageResult> ToResultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplyOutputSettings();

        ImageFormat outputFormat = _targetFormat ?? SourceFormat;
        await using MemoryStream ms = new();
        await _image.WriteAsync(ms, MagickFormatMapper.ToMagickFormat(outputFormat), cancellationToken).ConfigureAwait(false);

        RecordMetrics(outputFormat);

        ImageResult result = new(
            ms.ToArray(),
            outputFormat,
            (int)_image.Width,
            (int)_image.Height);

        return result;
    }

    /// <inheritdoc/>
    public async Task SaveToStreamAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplyOutputSettings();

        ImageFormat outputFormat = _targetFormat ?? SourceFormat;
        await _image.WriteAsync(destination, MagickFormatMapper.ToMagickFormat(outputFormat), cancellationToken).ConfigureAwait(false);

        RecordMetrics(outputFormat);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _image.Dispose();
        return ValueTask.CompletedTask;
    }

    private void RecordMetrics(ImageFormat outputFormat)
    {
        string formatName = outputFormat.ToString().ToLowerInvariant();
        _metrics.RecordImageProcessed(tenantId: null, formatName);
        _metrics.RecordProcessingDuration(tenantId: null, formatName, Stopwatch.GetElapsedTime(_startTimestamp));
    }

    private void ApplyOutputSettings()
    {
        if (_quality.HasValue)
        {
            _image.Quality = (uint)_quality.Value;
        }
    }

    private void ApplyWatermark(MagickImage overlay, WatermarkPosition position, float opacity)
    {
        overlay.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, opacity);
        _image.Composite(overlay, ToGravity(position), CompositeOperator.Over);
    }

    private static Gravity ToGravity(WatermarkPosition position) => position switch
    {
        WatermarkPosition.Center => Gravity.Center,
        WatermarkPosition.TopLeft => Gravity.Northwest,
        WatermarkPosition.TopRight => Gravity.Northeast,
        WatermarkPosition.BottomLeft => Gravity.Southwest,
        WatermarkPosition.BottomRight => Gravity.Southeast,
        _ => Gravity.Center,
    };
}
