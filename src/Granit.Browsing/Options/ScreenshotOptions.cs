namespace Granit.Browsing.Options;

/// <summary>Options for <see cref="IBrowserPage.ScreenshotAsync"/>.</summary>
public sealed record ScreenshotOptions
{
    /// <summary>Output image format. Defaults to <see cref="ScreenshotFormat.Png"/>.</summary>
    public ScreenshotFormat Format { get; init; } = ScreenshotFormat.Png;

    /// <summary>JPEG / WebP quality in [0, 100]. Ignored for PNG.</summary>
    public int? Quality { get; init; }

    /// <summary>When <c>true</c>, captures the full scroll height of the document instead of the viewport only.</summary>
    public bool FullPage { get; init; }

    /// <summary>When <c>true</c>, the screenshot is rendered with a transparent background where supported.</summary>
    public bool OmitBackground { get; init; }

    /// <summary>Optional clipping rectangle. <c>null</c> captures the entire visible area (or full page when <see cref="FullPage"/>).</summary>
    public ScreenshotClip? Clip { get; init; }
}

/// <summary>Image format for a screenshot capture.</summary>
public enum ScreenshotFormat
{
    /// <summary>Lossless PNG.</summary>
    Png,

    /// <summary>Lossy JPEG. Pair with <see cref="ScreenshotOptions.Quality"/>.</summary>
    Jpeg,

    /// <summary>WebP. Pair with <see cref="ScreenshotOptions.Quality"/>. Engine support varies.</summary>
    Webp,
}

/// <summary>Rectangular clip applied before encoding.</summary>
/// <param name="X">X-coordinate of the top-left corner in CSS pixels.</param>
/// <param name="Y">Y-coordinate of the top-left corner in CSS pixels.</param>
/// <param name="Width">Width in CSS pixels.</param>
/// <param name="Height">Height in CSS pixels.</param>
public sealed record ScreenshotClip(double X, double Y, double Width, double Height);
