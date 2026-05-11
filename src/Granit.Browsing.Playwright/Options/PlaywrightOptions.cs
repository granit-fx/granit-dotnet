namespace Granit.Browsing.Playwright.Options;

/// <summary>Playwright-specific knobs that complement the engine-agnostic <c>GranitBrowsingOptions</c>.</summary>
public sealed class PlaywrightOptions
{
    /// <summary>Configuration section key (<c>"Browsing:Playwright"</c>).</summary>
    public const string SectionName = "Browsing:Playwright";

    /// <summary>Browser engine to launch. Defaults to <see cref="BrowserEngine.Chromium"/> for PDF support.</summary>
    public BrowserEngine Engine { get; set; } = BrowserEngine.Chromium;

    /// <summary>
    /// Optional override for the browser executable. <c>null</c> uses the binary
    /// installed by the <c>playwright install</c> CLI (handled by the bundled
    /// provisioning service when not skipped).
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Skip the bundled <c>Microsoft.Playwright.Program.Main(["install"])</c> auto-install
    /// at boot. Set this to <c>true</c> when the host image bundles browsers itself.
    /// </summary>
    public bool SkipBrowserInstall { get; set; }

    /// <summary>
    /// Tri-state opt-in for the bundled <c>playwright install</c> CLI invocation:
    /// <c>true</c> forces a download attempt at boot, <c>false</c> refuses to run the
    /// CLI (the host must pre-provision browsers), and <c>null</c> (the default) resolves
    /// to <c>!env.IsProduction()</c> — safe for dev/CI, locked down in production.
    /// </summary>
    public bool? AutoInstallBrowsers { get; set; }

    /// <summary>Extra command-line arguments forwarded to the underlying browser process.</summary>
    public string[] ExtraArgs { get; set; } = [];
}

/// <summary>Browser engines supported by Playwright.</summary>
public enum BrowserEngine
{
    /// <summary>Chromium — required for PDF generation and the native PDF viewer.</summary>
    Chromium,

    /// <summary>Firefox — no PDF generation.</summary>
    Firefox,

    /// <summary>WebKit (Safari) — no PDF generation.</summary>
    Webkit,
}
