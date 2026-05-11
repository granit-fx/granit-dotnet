using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Capabilities;

/// <summary>
/// Records the page session as an HTTP Archive (HAR) file. Useful for offline replay,
/// debugging slow third-party requests, and regulatory archival of page captures.
/// </summary>
public interface IHarRecordingCapability
{
    /// <summary>Begins capturing all network activity on <paramref name="page"/>.</summary>
    Task StartHarAsync(IBrowserPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the capture and returns the HAR document as a JSON string. Callers persist
    /// it (typically to <c>Granit.BlobStorage</c>) or feed it to an analyzer.
    /// </summary>
    Task<string> StopHarAsync(IBrowserPage page, CancellationToken cancellationToken = default);
}
