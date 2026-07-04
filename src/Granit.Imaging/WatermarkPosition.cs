namespace Granit.Imaging;

/// <summary>
/// Determines the placement of a watermark overlay on the target image.
/// </summary>
public enum WatermarkPosition
{
    /// <summary>Centered on the image.</summary>
    Center,

    /// <summary>Top-left corner.</summary>
    TopLeft,

    /// <summary>Top-right corner.</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom-right corner.</summary>
    BottomRight,
}
