namespace Granit.Browsing;

/// <summary>
/// Capability flags advertised by an <see cref="IHeadlessBrowser"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Engines differ — Chromium supports PDF generation natively, Firefox and WebKit do
/// not; Playwright supports tracing on every engine, PuppeteerSharp does not. Consumers
/// inspect <see cref="IHeadlessBrowser.Capabilities"/> (or call
/// <see cref="IHeadlessBrowser.Supports"/>) to gate optional features and resolve the
/// matching capability interface (<see cref="Capabilities.IPdfCapability"/>, etc.) from
/// DI when needed.
/// </para>
/// <para>
/// The flag set is the source of truth: a provider that does not advertise a capability
/// MUST NOT register the corresponding capability interface. Hosts that consume a
/// capability without it being advertised should fail at boot with a descriptive
/// exception, not at first request.
/// </para>
/// </remarks>
[System.Flags]
public enum BrowserCapabilities
{
    /// <summary>No capability advertised — typically a stub or test double.</summary>
    None = 0,

    /// <summary>Capture screenshots of a rendered page (PNG / JPEG).</summary>
    Screenshot = 1 << 0,

    /// <summary>Render the current page to a PDF document. Chromium-only in practice.</summary>
    PdfGeneration = 1 << 1,

    /// <summary>Intercept and modify network requests (block, mock, log).</summary>
    NetworkInterception = 1 << 2,

    /// <summary>Inject custom JavaScript into the rendered page.</summary>
    JavaScriptInjection = 1 << 3,

    /// <summary>Override viewport, user agent, device pixel ratio, locale, timezone.</summary>
    EmulateDevice = 1 << 4,

    /// <summary>Switch the CSS media type (<c>screen</c> vs <c>print</c>) and color scheme.</summary>
    EmulateMedia = 1 << 5,

    /// <summary>Override geolocation coordinates and grant the geolocation permission.</summary>
    Geolocation = 1 << 6,

    /// <summary>Load a PDF in the engine's native PDF viewer and screenshot pages individually. Chromium-only.</summary>
    PdfViewerNative = 1 << 7,

    /// <summary>Snapshot the rendered page's accessibility tree.</summary>
    AccessibilityTree = 1 << 8,

    /// <summary>Record a HAR (HTTP Archive) for the page session.</summary>
    HarRecording = 1 << 9,

    /// <summary>Record a Playwright-style trace (DOM snapshots, network, screencast).</summary>
    TraceRecording = 1 << 10,
}
