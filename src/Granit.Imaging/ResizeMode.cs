namespace Granit.Imaging;

/// <summary>
/// Determines how the image is resized to fit the target dimensions.
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// Fit within bounds, preserving aspect ratio. The result may be smaller
    /// than the target dimensions (no padding added).
    /// </summary>
    Max,

    /// <summary>
    /// Fit within bounds, preserving aspect ratio. The result is padded
    /// (with transparent pixels) to match the exact target dimensions.
    /// </summary>
    Pad,

    /// <summary>
    /// Fill the target dimensions exactly by resizing and cropping the overflow.
    /// The image is centered before cropping.
    /// </summary>
    Crop,

    /// <summary>
    /// Stretch the image to fill the target dimensions exactly.
    /// The aspect ratio is not preserved (image may appear distorted).
    /// </summary>
    Stretch,

    /// <summary>
    /// Resize to the minimum bounds, preserving aspect ratio. The result
    /// covers the target dimensions entirely (may exceed them).
    /// </summary>
    Min,
}
