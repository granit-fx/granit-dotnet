using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Capabilities;

/// <summary>
/// Records a Playwright-style trace (DOM snapshots, network activity, screencast).
/// Advertised by Playwright-backed providers only.
/// </summary>
public interface ITracingCapability
{
    /// <summary>Starts recording a trace on the supplied page.</summary>
    Task StartTracingAsync(IBrowserPage page, TracingOptions options, CancellationToken cancellationToken = default);

    /// <summary>Stops recording and returns the trace as an opaque archive (typically a ZIP).</summary>
    Task<byte[]> StopTracingAsync(IBrowserPage page, CancellationToken cancellationToken = default);
}

/// <summary>Options controlling what a Playwright trace captures.</summary>
public sealed record TracingOptions
{
    /// <summary>Capture DOM snapshots before/after each action.</summary>
    public bool Snapshots { get; init; } = true;

    /// <summary>Capture screenshots between actions.</summary>
    public bool Screenshots { get; init; } = true;

    /// <summary>Capture source frames for actions originating from script / fixture code.</summary>
    public bool Sources { get; init; }

    /// <summary>Optional human-readable label associated with the trace.</summary>
    public string? Title { get; init; }
}
