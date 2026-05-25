namespace Granit.Html;

/// <summary>
/// Converts HTML content to a clean plain-text representation suitable for the
/// <c>text/plain</c> part of a <c>multipart/alternative</c> email or for indexing.
/// </summary>
/// <remarks>
/// The default AngleSharp-backed implementation ships in <c>Granit.Html.AngleSharp</c>.
/// Hosts processing untrusted HTML MUST use an implementation configured WITHOUT
/// an external resource loader (SSRF guard).
/// </remarks>
public interface IHtmlToPlainTextConverter
{
    /// <summary>
    /// Converts <paramref name="html"/> to plain text. Returns an empty string when the
    /// input is <c>null</c>, empty, or whitespace-only.
    /// </summary>
    Task<string> ConvertAsync(string html, CancellationToken cancellationToken = default);
}
