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
}
