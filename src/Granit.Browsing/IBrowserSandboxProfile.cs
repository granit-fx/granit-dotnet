using System;
using System.Collections.Generic;

namespace Granit.Browsing;

/// <summary>
/// Declarative sandbox profile applied at page-acquisition time. The single source of
/// truth for scheme, host, CSP and script policy in <c>Granit.Browsing</c>.
/// </summary>
/// <remarks>
/// <para>
/// Hosts register a profile with <c>services.AddSingleton&lt;IBrowserSandboxProfile&gt;(...)</c>
/// or scope it per consumer with a keyed registration (provider-specific). The provider
/// applies the profile through its native equivalents — request interception for
/// <see cref="BlockNetworkRequests"/>, <c>javaScriptEnabled = false</c> for
/// <see cref="DisableJavaScript"/>, etc.
/// </para>
/// <para>
/// The default registration is <c>DefaultSandboxProfile</c> (deny-by-default — HTTPS-only,
/// no private networks, CSP forced, console redacted, 30 s render cap). Callers opt out
/// rather than opt in. Conforms to OWASP ASVS V12.6.1 (SSRF defenses) and
/// ISO 27001 A.5.34 / A.8.27.
/// </para>
/// </remarks>
public interface IBrowserSandboxProfile
{
    /// <summary>When <c>true</c>, the page is opened with JavaScript disabled.</summary>
    bool DisableJavaScript { get; }

    /// <summary>When <c>true</c>, the page cannot issue network requests (other than the initial document load).</summary>
    bool BlockNetworkRequests { get; }

    /// <summary>When <c>true</c>, image loading is suppressed — useful when only DOM structure is needed.</summary>
    bool DisableImages { get; }

    /// <summary>
    /// URL pattern list (provider-defined glob syntax) blocked unconditionally. Use to
    /// drop tracker domains while keeping the rest of the network open. Combined with
    /// <see cref="DeniedHostPatterns"/> additively.
    /// </summary>
    IReadOnlyList<string>? BlockedUrlPatterns { get; }

    /// <summary>
    /// Schemes the page is permitted to navigate to or fetch from. Default
    /// <c>["https"]</c> in <c>DefaultSandboxProfile</c>. A request whose scheme is not in
    /// the list is short-circuited with
    /// <see cref="Sandbox.SandboxViolationKind.SchemeNotAllowed"/>.
    /// </summary>
    IReadOnlyList<string> AllowedSchemes { get; }

    /// <summary>
    /// When <c>true</c>, the request router re-resolves every host at request time and
    /// blocks any request that resolves to a private / loopback / link-local / cloud
    /// metadata / IPv6 ULA address. Defeats DNS rebinding between page acquisition and
    /// navigation. Default <c>true</c>.
    /// </summary>
    bool BlockPrivateNetworks { get; }

    /// <summary>
    /// Optional allowlist of host patterns (glob syntax compatible with
    /// <c>Microsoft.Extensions.FileSystemGlobbing</c>). When non-<c>null</c>, only hosts
    /// matching at least one pattern are allowed. <c>null</c> means "any public host".
    /// </summary>
    IReadOnlyList<string>? AllowedHostPatterns { get; }

    /// <summary>
    /// Optional denylist of host patterns (glob syntax). A request whose host matches any
    /// pattern is short-circuited regardless of <see cref="AllowedHostPatterns"/>.
    /// Additive to <see cref="BlockedUrlPatterns"/>.
    /// </summary>
    IReadOnlyList<string>? DeniedHostPatterns { get; }

    /// <summary>
    /// When <c>true</c>, the provider MUST refuse any caller request to bypass the page's
    /// Content-Security-Policy and MUST require the
    /// <c>Granit.Browsing.Pages.BypassCsp</c> permission for any opt-in. Default
    /// <c>true</c>. Overrides any historical per-page bypass flag.
    /// </summary>
    bool ForceCsp { get; }

    /// <summary>
    /// When <c>true</c>, the provider runs every console message through
    /// <c>ConsoleRedactor</c> before surfacing it via <see cref="IBrowserPage.ConsoleMessages"/>.
    /// Default <c>true</c>. Closes the bearer-token / Set-Cookie leak in
    /// VULN-201.
    /// </summary>
    bool RedactConsoleMessages { get; }

    /// <summary>
    /// Hard cap on the duration of any single render operation (navigate, screenshot,
    /// PDF, etc.). Overrides <c>GranitBrowsingOptions.ResourceLimits.MaxRenderDuration</c>
    /// when set. <c>null</c> defers to the options value.
    /// </summary>
    TimeSpan? MaxRenderDuration { get; }

    /// <summary>
    /// Optional allowlist prefix for the provider's executable path (e.g.
    /// <c>"/usr/lib/chromium/"</c>). The provider refuses to spawn a browser process
    /// whose executable does not start with this prefix, preventing path traversal
    /// or weaponised local-fetch payloads. <c>null</c> disables the check.
    /// </summary>
    string? AllowedExecutablePathPrefix { get; }
}
