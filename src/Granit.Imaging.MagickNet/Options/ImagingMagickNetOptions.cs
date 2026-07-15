namespace Granit.Imaging.MagickNet.Options;

/// <summary>
/// Configuration options for Magick.NET image processing security and resource limits.
/// </summary>
/// <remarks>
/// Bound to the <c>Imaging:MagickNet</c> configuration section and validated at startup.
/// Resource limits are applied globally to the ImageMagick native library when the host
/// starts (see <c>MagickNetResourceLimitsInitializer</c>) — they are process-wide state,
/// not per-registration. Set a value to <c>0</c> to use the ImageMagick default (unlimited).
/// </remarks>
public sealed class ImagingMagickNetOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Imaging:MagickNet";

    /// <summary>
    /// Maximum memory in bytes for ImageMagick pixel cache. Default: 256 MB.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 256 * 1024 * 1024;

    /// <summary>
    /// Maximum image width in pixels. Default: 16384 (16K).
    /// </summary>
    public int MaxWidthPixels { get; set; } = 16384;

    /// <summary>
    /// Maximum image height in pixels. Default: 16384 (16K).
    /// </summary>
    public int MaxHeightPixels { get; set; } = 16384;

    /// <summary>
    /// Maximum number of images in a sequence (e.g., GIF animation frames). Default: 32.
    /// </summary>
    public int MaxListLength { get; set; } = 32;

    /// <summary>
    /// Maximum input file size in bytes before decoding. Default: 50 MB.
    /// Set to <c>0</c> to disable input size validation.
    /// </summary>
    public long MaxInputBytes { get; set; } = 50 * 1024 * 1024;
}
