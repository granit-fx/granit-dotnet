namespace Granit.Browsing.PuppeteerSharp.Options;

/// <summary>
/// PuppeteerSharp-specific knobs that complement the engine-agnostic
/// <c>GranitBrowsingOptions</c>.
/// </summary>
public sealed class PuppeteerSharpOptions
{
    /// <summary>Configuration section key (<c>"Browsing:PuppeteerSharp"</c>).</summary>
    public const string SectionName = "Browsing:PuppeteerSharp";

    /// <summary>
    /// Optional override for the Chromium executable. <c>null</c> tells PuppeteerSharp
    /// to download and manage its own Chromium build (recommended for first-run /
    /// dev environments; production hosts typically point this at a system Chromium
    /// in a hardened container).
    /// </summary>
    public string? ChromiumExecutablePath { get; set; }

    /// <summary>
    /// Disables the Chromium sandbox (<c>--no-sandbox</c>, <c>--disable-setuid-sandbox</c>).
    /// Required when running as root inside an unprivileged container that can't
    /// create user namespaces. <b>Reduces process isolation</b> — leave <c>false</c>
    /// in any environment that can keep the sandbox on.
    /// </summary>
    public bool DisableSandbox { get; set; }

    /// <summary>
    /// Skip the first-run Chromium download driven by <c>BrowserFetcher</c>. Set this
    /// to <c>true</c> when the host image bundles its own Chromium (typical CI / prod)
    /// and pair with <see cref="ChromiumExecutablePath"/>.
    /// </summary>
    public bool SkipChromiumDownload { get; set; }

    /// <summary>
    /// Extra command-line arguments forwarded to the underlying Chromium process.
    /// Useful for tuning <c>--disk-cache-size</c>, locale flags, or proxy settings.
    /// </summary>
    public string[] ExtraArgs { get; set; } = [];
}
