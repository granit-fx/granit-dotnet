namespace Granit.TextExtraction.Exceptions;

/// <summary>
/// Raised when an extraction stage detects an unrecoverable condition — most commonly an
/// input stream exceeding <see cref="Granit.TextExtraction.Options.GranitTextExtractionOptions.MaxBodySizeBytes"/>
/// (zip-bomb / unbounded-upload defence). NEVER raised for soft caps (truncation is signalled
/// via <see cref="TextExtractionResult.IsTruncated"/>).
/// </summary>
/// <remarks>
/// <see cref="Reason"/> uses snake_case stable identifiers (e.g. <c>input_too_large</c>,
/// <c>archive_too_many_entries</c>) so consumers can branch on it without parsing the message.
/// </remarks>
public sealed class TextExtractionException : Exception
{
    /// <summary>Stable snake_case identifier for the failure cause.</summary>
    public string Reason { get; }

    public TextExtractionException(string reason)
        : base(reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Reason = reason;
    }

    public TextExtractionException(string reason, Exception innerException)
        : base(reason, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Reason = reason;
    }
}
