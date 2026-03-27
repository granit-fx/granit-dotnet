namespace Granit.BlobStorage.Internal;

/// <summary>
/// Sanitizes filenames for use in HTTP <c>Content-Disposition</c> headers
/// to prevent header injection attacks (RFC 6266).
/// </summary>
public static class ContentDispositionHelper
{
    /// <summary>
    /// Builds a safe <c>Content-Disposition: attachment</c> header value from an untrusted filename.
    /// Strips path components, removes structural characters (<c>"</c>, <c>\</c>, CRLF),
    /// and produces both an ASCII <c>filename=</c> and a UTF-8 <c>filename*=</c> parameter
    /// per RFC 5987 for non-ASCII names.
    /// </summary>
    public static string BuildAttachmentHeader(string fileName)
    {
        // Strip path components to prevent directory traversal in the filename.
        string safe = Path.GetFileName(fileName);

        // Remove characters that are structural in Content-Disposition (RFC 6266 §4.3).
        safe = safe.Replace("\"", "'")
                   .Replace("\\", "_")
                   .Replace("\r", "")
                   .Replace("\n", "");

        // Fallback for empty result after sanitization.
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "download";
        }

        return $"attachment; filename=\"{safe}\"; filename*=UTF-8''{Uri.EscapeDataString(safe)}";
    }
}
