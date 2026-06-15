namespace Granit.Imaging;

/// <summary>
/// Header-only metadata about an image, read without decoding the pixel buffer.
/// </summary>
/// <param name="Size">The image dimensions in pixels.</param>
/// <param name="Format">The detected image format.</param>
public readonly record struct ImageInfo(ImageSize Size, ImageFormat Format);
