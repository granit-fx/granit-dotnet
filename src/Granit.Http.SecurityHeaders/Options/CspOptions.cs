namespace Granit.Http.SecurityHeaders.Options;

/// <summary>
/// Typed Content-Security-Policy base configuration. Bindable from the
/// <c>SecurityHeaders:Csp</c> section.
/// </summary>
/// <remarks>
/// <para>
/// The composer starts from these base directives and lets every matching
/// <see cref="ICspContributor"/> layer its own sources on top. Property
/// names are explicit (no <c>IDictionary</c>) so <c>appsettings.json</c>
/// bindings are typo-safe and env-var binding works without quirks around
/// hyphenated keys.
/// </para>
/// <para>
/// The default base policy is API-grade strict: <c>default-src 'none'</c>,
/// <c>base-uri 'none'</c>, <c>frame-ancestors 'none'</c>. All other
/// directives are empty by default and inherit from <c>default-src 'none'</c>
/// per CSP semantics — appropriate for JSON-only endpoints.
/// </para>
/// </remarks>
public sealed class CspOptions
{
    /// <summary>Base sources for <c>default-src</c>. Default: <c>['none']</c>.</summary>
    public IList<string> DefaultSrc { get; set; } = ["'none'"];

    /// <summary>Base sources for <c>script-src</c>. Empty by default (inherits <c>default-src</c>).</summary>
    public IList<string> ScriptSrc { get; set; } = [];

    /// <summary>Base sources for <c>script-src-elem</c>. Empty by default.</summary>
    public IList<string> ScriptSrcElem { get; set; } = [];

    /// <summary>Base sources for <c>script-src-attr</c>. Empty by default.</summary>
    public IList<string> ScriptSrcAttr { get; set; } = [];

    /// <summary>Base sources for <c>style-src</c>. Empty by default.</summary>
    public IList<string> StyleSrc { get; set; } = [];

    /// <summary>Base sources for <c>style-src-elem</c>. Empty by default.</summary>
    public IList<string> StyleSrcElem { get; set; } = [];

    /// <summary>Base sources for <c>style-src-attr</c>. Empty by default.</summary>
    public IList<string> StyleSrcAttr { get; set; } = [];

    /// <summary>Base sources for <c>font-src</c>. Empty by default.</summary>
    public IList<string> FontSrc { get; set; } = [];

    /// <summary>Base sources for <c>img-src</c>. Empty by default.</summary>
    public IList<string> ImgSrc { get; set; } = [];

    /// <summary>Base sources for <c>connect-src</c>. Empty by default.</summary>
    public IList<string> ConnectSrc { get; set; } = [];

    /// <summary>Base sources for <c>frame-src</c>. Empty by default.</summary>
    public IList<string> FrameSrc { get; set; } = [];

    /// <summary>Base sources for <c>worker-src</c>. Empty by default.</summary>
    public IList<string> WorkerSrc { get; set; } = [];

    /// <summary>Base sources for <c>media-src</c>. Empty by default.</summary>
    public IList<string> MediaSrc { get; set; } = [];

    /// <summary>Base sources for <c>object-src</c>. Empty by default.</summary>
    public IList<string> ObjectSrc { get; set; } = [];

    /// <summary>Base sources for <c>manifest-src</c>. Empty by default.</summary>
    public IList<string> ManifestSrc { get; set; } = [];

    /// <summary>Base sources for <c>child-src</c>. Empty by default.</summary>
    public IList<string> ChildSrc { get; set; } = [];

    /// <summary>Base sources for <c>base-uri</c>. Default: <c>['none']</c>.</summary>
    public IList<string> BaseUri { get; set; } = ["'none'"];

    /// <summary>Base sources for <c>form-action</c>. Empty by default.</summary>
    public IList<string> FormAction { get; set; } = [];

    /// <summary>Base sources for <c>frame-ancestors</c>. Default: <c>['none']</c>.</summary>
    public IList<string> FrameAncestors { get; set; } = ["'none'"];

    /// <summary>
    /// Emergency escape hatch. When non-null, this string is emitted verbatim
    /// as the <c>Content-Security-Policy</c> header and the composer skips
    /// every directive list and every contributor. Triggers a
    /// <c>LogWarning</c> at startup so operators surface the bypass.
    /// </summary>
    public string? RawOverride { get; set; }

    /// <summary>
    /// When <c>true</c>, emits <c>Content-Security-Policy-Report-Only</c>
    /// instead of the enforcing header. Use for staged rollout of a new
    /// policy without breaking the site.
    /// </summary>
    public bool ReportOnly { get; set; }

    /// <summary>
    /// Sets the <c>report-uri</c> endpoint for CSP violation reports.
    /// </summary>
    public string? ReportUri { get; set; }

    /// <summary>
    /// Sets the <c>report-to</c> reporting group name (Reporting API).
    /// </summary>
    public string? ReportTo { get; set; }

    /// <summary>
    /// When <c>true</c>, emits the <c>upgrade-insecure-requests</c>
    /// directive.
    /// </summary>
    public bool UpgradeInsecureRequests { get; set; }
}
