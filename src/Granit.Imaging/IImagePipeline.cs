namespace Granit.Imaging;

/// <summary>
/// Fluent pipeline for chaining image transformation operations.
/// </summary>
/// <remarks>
/// Created by <see cref="IImageProcessor.LoadAsync(Stream, CancellationToken)"/> or
/// <see cref="IImageProcessor.Load(ReadOnlyMemory{byte})"/>. Each transformation method
/// mutates the underlying image and returns <see langword="this"/> for chaining.
/// Call a terminal method (<see cref="ToResultAsync"/> or <see cref="SaveToStreamAsync"/>)
/// to produce the output.
/// <para>
/// The pipeline holds native resources and must be disposed after use:
/// <code>
/// await using IImagePipeline pipeline = processor.Load(stream);
/// ImageResult result = await pipeline
///     .Resize(800, 600, ResizeMode.Crop)
///     .StripMetadata()
///     .Compress(quality: 75)
///     .ToResultAsync();
/// </code>
/// </para>
/// </remarks>
public interface IImagePipeline : IAsyncDisposable
{
    /// <summary>
    /// The dimensions of the source image as loaded (before any transformation).
    /// </summary>
    ImageSize SourceSize { get; }

    /// <summary>
    /// The detected format of the source image.
    /// </summary>
    ImageFormat SourceFormat { get; }

    /// <summary>
    /// Resizes the image to fit the specified dimensions using the given <paramref name="mode"/>.
    /// </summary>
    /// <param name="width">Target width in pixels.</param>
    /// <param name="height">Target height in pixels.</param>
    /// <param name="mode">The resize strategy (default: <see cref="ResizeMode.Max"/>).</param>
    /// <returns>This pipeline for chaining.</returns>
    IImagePipeline Resize(int width, int height, ResizeMode mode = ResizeMode.Max);

    /// <summary>
    /// Crops the image to the specified rectangular region.
    /// </summary>
    /// <param name="rectangle">The crop region.</param>
    /// <returns>This pipeline for chaining.</returns>
    IImagePipeline Crop(CropRectangle rectangle);

    /// <summary>
    /// Sets the output compression quality (0–100). Applied during encoding
    /// in the terminal operation. If not called, the format default quality is used.
    /// </summary>
    /// <param name="quality">Quality level from 0 (lowest) to 100 (highest).</param>
    /// <returns>This pipeline for chaining.</returns>
    IImagePipeline Compress(int quality);

    /// <summary>
    /// Sets the output format. If not called, the source format is preserved.
    /// </summary>
    /// <param name="format">The target image format.</param>
    /// <returns>This pipeline for chaining.</returns>
    IImagePipeline ConvertTo(ImageFormat format);

    /// <summary>
    /// Composites a watermark overlay onto the image.
    /// </summary>
    /// <param name="watermark">The watermark image data.</param>
    /// <param name="position">Placement on the target image (default: <see cref="WatermarkPosition.BottomRight"/>).</param>
    /// <param name="opacity">Opacity from 0.0 (invisible) to 1.0 (opaque). Default: 0.5.</param>
    /// <returns>This pipeline for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside the [0, 1] range.</exception>
    IImagePipeline Watermark(
        ReadOnlyMemory<byte> watermark,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = 0.5f);

    /// <summary>
    /// Composites a watermark overlay onto the image.
    /// </summary>
    /// <param name="watermark">A stream containing the watermark image.</param>
    /// <param name="position">Placement on the target image (default: <see cref="WatermarkPosition.BottomRight"/>).</param>
    /// <param name="opacity">Opacity from 0.0 (invisible) to 1.0 (opaque). Default: 0.5.</param>
    /// <returns>This pipeline for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="opacity"/> is outside the [0, 1] range.</exception>
    IImagePipeline Watermark(
        Stream watermark,
        WatermarkPosition position = WatermarkPosition.BottomRight,
        float opacity = 0.5f);

    /// <summary>
    /// Strips all metadata (EXIF, IPTC, XMP) from the image.
    /// Useful for GDPR compliance and reducing file size.
    /// </summary>
    /// <returns>This pipeline for chaining.</returns>
    IImagePipeline StripMetadata();

    /// <summary>
    /// Encodes the image and returns the result as an <see cref="ImageResult"/>.
    /// Uses the format set by <see cref="ConvertTo"/> or the source format if not set.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed image result.</returns>
    Task<ImageResult> ToResultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Encodes the image and writes it to the specified <paramref name="destination"/> stream.
    /// Uses the format set by <see cref="ConvertTo"/> or the source format if not set.
    /// </summary>
    /// <param name="destination">The stream to write the encoded image to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveToStreamAsync(Stream destination, CancellationToken cancellationToken = default);
}
