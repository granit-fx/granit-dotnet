using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Capabilities;

/// <summary>
/// Renders a page to a PDF document. Advertised by Chromium-backed providers only —
/// Firefox and WebKit lack PDF generation entirely. Hosts that consume this interface
/// without a Chromium provider registered fail at boot.
/// </summary>
/// <remarks>
/// Consumed by <c>Granit.DocumentGeneration.Pdf</c> after F3 lands; future consumers
/// (long-form report exports, regulatory archive generation) inject the same surface.
/// </remarks>
public interface IPdfCapability
{
    /// <summary>
    /// Renders the current document of <paramref name="page"/> to a PDF byte array.
    /// </summary>
    /// <param name="page">Page whose content is rendered. Caller is responsible for setting up the document (navigate / set content) before invoking this method.</param>
    /// <param name="options">Layout, page size, margins, headers/footers, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PDF bytes ready to be persisted, streamed, or passed to a PDF/A converter.</returns>
    Task<byte[]> RenderToPdfAsync(IBrowserPage page, PdfOptions options, CancellationToken cancellationToken = default);
}
