namespace Granit.AI.Vision;

/// <summary>
/// The sentinel envelope shared by Granit's vision-OCR callers
/// (<c>Granit.TextExtraction.Ocr.AI</c> and <c>Granit.Imaging.AI</c>): the model is asked to
/// wrap its transcription between the markers, and <see cref="Extract"/> drops everything
/// outside them — compliant chatter, prefatory rationalisations, or an injection payload
/// synthesised from text embedded in the image (OWASP LLM01).
/// </summary>
public static class VisionOcrEnvelope
{
    /// <summary>Opening sentinel marker.</summary>
    public const string Open = "<granit-vlm-ocr>";

    /// <summary>Closing sentinel marker.</summary>
    public const string Close = "</granit-vlm-ocr>";

    /// <summary>
    /// Strips the envelope from a raw model response. Falls back to the trimmed raw text
    /// when the model failed to emit markers, so callers still get SOMETHING indexable
    /// instead of a silent empty result; with an open marker but no close, everything after
    /// the open marker is kept (cheaper than re-prompting, still surfaces the bulk).
    /// </summary>
    /// <param name="raw">The raw model response text.</param>
    /// <returns>The envelope body, trimmed.</returns>
    public static string Extract(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        int openIdx = raw.IndexOf(Open, StringComparison.Ordinal);
        if (openIdx < 0)
        {
            return raw.Trim();
        }

        int bodyStart = openIdx + Open.Length;
        int closeIdx = raw.IndexOf(Close, bodyStart, StringComparison.Ordinal);
        if (closeIdx < 0)
        {
            return raw[bodyStart..].Trim();
        }

        return raw[bodyStart..closeIdx].Trim();
    }
}
