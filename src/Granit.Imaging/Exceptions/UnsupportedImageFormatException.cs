namespace Granit.Imaging.Exceptions;

/// <summary>
/// Exception thrown when the image format is not recognized or not supported.
/// </summary>
public sealed class UnsupportedImageFormatException : Exception
{
    /// <summary>
    /// The format string that was not recognized.
    /// </summary>
    public string DetectedFormat { get; }

    /// <summary>
    /// Initializes a new <see cref="UnsupportedImageFormatException"/>.
    /// </summary>
    /// <param name="detectedFormat">The format identifier that could not be mapped.</param>
    public UnsupportedImageFormatException(string detectedFormat)
        : base("The uploaded image format is not supported.") =>
        DetectedFormat = detectedFormat;

    /// <summary>
    /// Initializes a new <see cref="UnsupportedImageFormatException"/> wrapping the
    /// underlying decoder failure (e.g. a corrupt or truncated header).
    /// </summary>
    /// <param name="detectedFormat">The format identifier that could not be mapped.</param>
    /// <param name="innerException">The decoder exception that triggered this failure.</param>
    public UnsupportedImageFormatException(string detectedFormat, Exception innerException)
        : base("The uploaded image format is not supported.", innerException) =>
        DetectedFormat = detectedFormat;
}
