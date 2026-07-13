// ReSharper disable once CheckNamespace
namespace Granit.Http.SecurityHeaders.Options;

/// <summary>
/// Configuration options for Granit HTTP security headers.
/// Bindable from <c>appsettings.json</c> section <c>"Http:SecurityHeaders"</c>.
/// </summary>
public sealed class GranitSecurityHeadersOptions
{
    /// <summary>
    /// Configuration section name in <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "Http:SecurityHeaders";

    // -------------------------------------------------------------------------
    // Server fingerprinting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Suppresses the Kestrel <c>Server</c> response header.
    /// Default: <c>true</c>.
    /// </summary>
    public bool SuppressServerHeader { get; set; } = true;

    // -------------------------------------------------------------------------
    // OWASP recommended headers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds <c>X-Content-Type-Options: nosniff</c> to prevent MIME-type sniffing.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableContentTypeOptions { get; set; } = true;

    /// <summary>
    /// Sets <c>X-Frame-Options</c> to prevent clickjacking.
    /// Default: <c>"DENY"</c>. Set to <c>"SAMEORIGIN"</c> if the application uses iframes.
    /// Set to <c>null</c> to disable (when using <c>Content-Security-Policy: frame-ancestors</c> instead).
    /// </summary>
    public string? XFrameOptions { get; set; } = "DENY";

    /// <summary>
    /// Sets <c>Referrer-Policy</c> to control referrer information leakage.
    /// Default: <c>"strict-origin-when-cross-origin"</c>.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Sets <c>X-XSS-Protection: 0</c> to disable the legacy XSS auditor in older browsers.
    /// The XSS auditor is deprecated and can introduce vulnerabilities; disabling it is recommended.
    /// Default: <c>true</c>.
    /// </summary>
    public bool DisableXssProtection { get; set; } = true;

    /// <summary>
    /// Sets <c>Permissions-Policy</c> to restrict browser features.
    /// Default restricts camera, microphone, geolocation, payment, accelerometer, gyroscope,
    /// magnetometer, and USB per the OWASP Secure Headers Project.
    /// Set to <c>null</c> to omit.
    /// </summary>
    public string? PermissionsPolicy { get; set; } =
        "camera=(), microphone=(), geolocation=(), payment=(), " +
        "accelerometer=(), gyroscope=(), magnetometer=(), usb=()";

    /// <summary>
    /// Typed Content-Security-Policy base configuration. Default: API-grade
    /// strict CSP — <c>default-src 'none'</c>, <c>base-uri 'none'</c>,
    /// <c>frame-ancestors 'none'</c>.
    /// </summary>
    /// <remarks>
    /// The composer starts from these directives and lets every registered
    /// <see cref="ICspContributor"/> layer additional sources on top for
    /// requests matching its scope. To inject ad-hoc overrides at deployment
    /// time, set <see cref="CspOptions.RawOverride"/> — that bypasses the
    /// composer entirely and emits the raw string as the CSP header.
    /// </remarks>
    public CspOptions Csp { get; set; } = new();

    /// <summary>
    /// Names (matching <see cref="ICspContributor.Name"/>) of contributors
    /// that must be ignored even when registered. Bind from
    /// <c>Http:SecurityHeaders:DisabledContributors</c> to disable a framework
    /// contributor when an internal security policy is stricter than the
    /// framework's default.
    /// </summary>
    public IReadOnlyList<string> DisabledContributors { get; set; } = [];

    // -------------------------------------------------------------------------
    // HSTS (HTTP Strict Transport Security)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enables <c>Strict-Transport-Security</c> header via ASP.NET Core HSTS middleware.
    /// Default: <c>true</c>.
    /// </summary>
    /// <remarks>
    /// HSTS is only sent over HTTPS. In development (<c>ASPNETCORE_ENVIRONMENT=Development</c>),
    /// ASP.NET Core skips HSTS automatically.
    /// </remarks>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// HSTS <c>max-age</c> in seconds.
    /// Default: <c>31536000</c> (1 year, OWASP recommendation).
    /// </summary>
    public int HstsMaxAgeSeconds { get; set; } = 31_536_000;

    /// <summary>
    /// Includes <c>includeSubDomains</c> in the HSTS header.
    /// Default: <c>true</c>.
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; } = true;

    /// <summary>
    /// Includes <c>preload</c> directive for HSTS Preload List submission.
    /// Default: <c>false</c> (requires domain-wide HTTPS commitment).
    /// </summary>
    public bool HstsPreload { get; set; }

    // -------------------------------------------------------------------------
    // Cross-Origin policies (Spectre mitigation)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets <c>Cross-Origin-Opener-Policy</c> to isolate the browsing context.
    /// Default: <c>"same-origin"</c>.
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>
    /// Sets <c>Cross-Origin-Embedder-Policy</c>.
    /// Default: <c>null</c> (not set — can break cross-origin resources like Google Fonts).
    /// </summary>
    /// <remarks>
    /// Set to <c>"require-corp"</c> for full Spectre isolation (enables <c>SharedArrayBuffer</c>).
    /// Only enable if all cross-origin resources include <c>Cross-Origin-Resource-Policy</c>.
    /// </remarks>
    public string? CrossOriginEmbedderPolicy { get; set; }

    /// <summary>
    /// Sets <c>Cross-Origin-Resource-Policy</c> to control resource sharing.
    /// Default: <c>"same-origin"</c>.
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";
}
