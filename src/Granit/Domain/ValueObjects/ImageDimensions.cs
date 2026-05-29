using System.Text.Json.Serialization;

namespace Granit.Domain.ValueObjects;

/// <summary>
/// Pixel dimensions of a raster image — width and height. A reusable value object
/// for any domain that records image size: media libraries, document renditions,
/// Open Graph / social cards, thumbnails, avatars.
/// </summary>
/// <remarks>
/// Equality is structural over <see cref="Width"/> and <see cref="Height"/>. The
/// derived geometry members (<see cref="AspectRatio"/>, orientation flags,
/// <see cref="TotalPixels"/>) are computed and excluded from JSON so a serialized
/// value stays the minimal <c>{ "width": …, "height": … }</c> shape.
/// </remarks>
public sealed class ImageDimensions : ValueObject
{
    /// <summary>Creates validated, non-negative pixel dimensions.</summary>
    /// <param name="width">Width in pixels (non-negative).</param>
    /// <param name="height">Height in pixels (non-negative).</param>
    /// <exception cref="ArgumentOutOfRangeException">When a dimension is negative.</exception>
    public ImageDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
    }

    /// <summary>Width in pixels (non-negative).</summary>
    public int Width { get; }

    /// <summary>Height in pixels (non-negative).</summary>
    public int Height { get; }

    /// <summary>
    /// Width divided by height. Returns <c>0</c> when <see cref="Height"/> is <c>0</c>
    /// (degenerate) rather than producing infinity / NaN.
    /// </summary>
    [JsonIgnore]
    public double AspectRatio => Height == 0 ? 0d : (double)Width / Height;

    /// <summary><c>true</c> when wider than tall.</summary>
    [JsonIgnore]
    public bool IsLandscape => Width > Height;

    /// <summary><c>true</c> when taller than wide.</summary>
    [JsonIgnore]
    public bool IsPortrait => Height > Width;

    /// <summary><c>true</c> when width equals height.</summary>
    [JsonIgnore]
    public bool IsSquare => Width == Height;

    /// <summary>Total pixel count (<see cref="long"/> so large images cannot overflow).</summary>
    [JsonIgnore]
    public long TotalPixels => (long)Width * Height;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Width;
        yield return Height;
    }

    /// <summary>Human-readable form, e.g. <c>1200x630px</c>.</summary>
    public override string ToString() => $"{Width}x{Height}px";
}
