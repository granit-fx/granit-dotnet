namespace Granit.Imaging;

/// <summary>
/// Entry point for image processing. Injected via dependency injection.
/// </summary>
/// <remarks>
/// Use <see cref="LoadAsync(Stream, CancellationToken)"/> or
/// <see cref="Load(ReadOnlyMemory{byte})"/> to create an <see cref="IImagePipeline"/> that
/// provides a fluent API for image transformations. Stream loading is asynchronous because
/// reading the source is I/O; the in-memory overloads stay synchronous — decoding is pure
/// CPU work and a <c>Task</c> wrapper would be dishonest.
/// <para>
/// The returned pipeline must be disposed after use to release native resources:
/// <code>
/// await using IImagePipeline pipeline = await imageProcessor.LoadAsync(stream);
/// ImageResult result = await pipeline
///     .Resize(800, 600, ResizeMode.Crop)
///     .Compress(quality: 75)
///     .SaveAsWebPAsync();
/// </code>
/// </para>
/// </remarks>
public interface IImageProcessor
{
    /// <summary>
    /// Loads an image from a <see cref="Stream"/> and returns a processing pipeline.
    /// Non-seekable streams are buffered asynchronously before decoding.
    /// </summary>
    /// <param name="source">The stream containing the image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fluent pipeline for chaining image operations.</returns>
    Task<IImagePipeline> LoadAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an image from a byte buffer and returns a processing pipeline.
    /// </summary>
    /// <param name="source">The byte buffer containing the image data.</param>
    /// <returns>A fluent pipeline for chaining image operations.</returns>
    IImagePipeline Load(ReadOnlyMemory<byte> source);

    /// <summary>
    /// Reads the image dimensions and format from the header WITHOUT decoding the pixel
    /// buffer. Use this as a pre-decode guard (e.g. pixel-bomb defence): it inspects only
    /// the format header, so a hostile file declaring enormous dimensions never allocates
    /// a decoded surface.
    /// </summary>
    /// <param name="source">The byte buffer containing the image data.</param>
    /// <returns>The header-only <see cref="ImageInfo"/>.</returns>
    /// <exception cref="Exceptions.UnsupportedImageFormatException">
    /// The data is not a recognized raster format, or its header cannot be read.
    /// </exception>
    ImageInfo Identify(ReadOnlyMemory<byte> source);
}
