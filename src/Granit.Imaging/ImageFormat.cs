namespace Granit.Imaging;

/// <summary>
/// Supported image formats for processing and output.
/// </summary>
public enum ImageFormat
{
    /// <summary>JPEG format — lossy compression, no transparency.</summary>
    Jpeg,

    /// <summary>PNG format — lossless compression, supports transparency.</summary>
    Png,

    /// <summary>WebP format — modern lossy/lossless, smaller than JPEG at equivalent quality.</summary>
    WebP,

    /// <summary>AVIF format — next-gen lossy/lossless based on AV1, best compression ratio.</summary>
    Avif,

    /// <summary>GIF format — limited to 256 colors, supports animation.</summary>
    Gif,

    /// <summary>BMP format — uncompressed bitmap.</summary>
    Bmp,

    /// <summary>TIFF format — lossless, used in print and medical imaging.</summary>
    Tiff,
}
