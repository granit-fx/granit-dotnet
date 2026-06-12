using System.Collections.ObjectModel;

namespace Granit.Http.SecurityHeaders;

/// <summary>
/// Mutable, fluent collector of <c>Content-Security-Policy</c> source
/// expressions and reporting hints for a single request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime.</b> One instance per request. The composer creates a fresh
/// builder, passes it in registration order to every matching
/// <see cref="ICspContributor.Contribute(Microsoft.AspNetCore.Http.HttpContext, CspBuilder)"/>
/// call, then reads the final state via the public <c>IReadOnly*</c> surface
/// and serialises the header value. Contributors append; they do not see the
/// emitted string.
/// </para>
/// <para>
/// <b>Thread-safety.</b> Instances are NOT thread-safe. Do not capture the
/// builder across requests; do not mutate it from concurrent async
/// continuations within the same <c>Contribute</c> call.
/// </para>
/// <para>
/// <b><c>'none'</c> semantics.</b> When a directive's source set ends up
/// containing <c>'none'</c> together with any other source, the composer
/// drops <c>'none'</c> at serialisation time (per CSP spec — <c>'none'</c>
/// cannot coexist with real sources in the same directive).
/// </para>
/// <para>
/// <b>Input validation.</b> Every <c>Add*</c>/<c>Set*</c> method rejects
/// sources that contain directive separators (<c>;</c>), CR/LF, NUL, or any
/// C0 control character. This prevents CSP injection where a contributor
/// interpolates user- or config-supplied data into a source.
/// </para>
/// <para>
/// <b>No <c>Build()</c>.</b> The builder exposes its state via the public
/// <see cref="Directives"/>, <see cref="ReportUri"/>, <see cref="ReportTo"/>,
/// and <see cref="UpgradeInsecureRequests"/> properties. The composer reads
/// them and applies the serialisation rules; alternative composers can be
/// written without <c>InternalsVisibleTo</c>.
/// </para>
/// </remarks>
public sealed class CspBuilder
{
    private readonly Dictionary<string, HashSet<string>> _directives = new(StringComparer.Ordinal);
    private string? _reportUri;
    private string? _reportTo;
    private bool _upgradeInsecureRequests;

    // --- Fetch directives -------------------------------------------------

    /// <summary>Appends source expressions to the <c>default-src</c> directive.</summary>
    public CspBuilder AddDefaultSrc(params string[] sources) => Add("default-src", sources);

    /// <summary>Appends source expressions to the <c>script-src</c> directive.</summary>
    public CspBuilder AddScriptSrc(params string[] sources) => Add("script-src", sources);

    /// <summary>Appends source expressions to the <c>script-src-elem</c> directive.</summary>
    public CspBuilder AddScriptSrcElem(params string[] sources) => Add("script-src-elem", sources);

    /// <summary>Appends source expressions to the <c>script-src-attr</c> directive.</summary>
    public CspBuilder AddScriptSrcAttr(params string[] sources) => Add("script-src-attr", sources);

    /// <summary>Appends source expressions to the <c>style-src</c> directive.</summary>
    public CspBuilder AddStyleSrc(params string[] sources) => Add("style-src", sources);

    /// <summary>Appends source expressions to the <c>style-src-elem</c> directive.</summary>
    public CspBuilder AddStyleSrcElem(params string[] sources) => Add("style-src-elem", sources);

    /// <summary>Appends source expressions to the <c>style-src-attr</c> directive.</summary>
    public CspBuilder AddStyleSrcAttr(params string[] sources) => Add("style-src-attr", sources);

    /// <summary>Appends source expressions to the <c>font-src</c> directive.</summary>
    public CspBuilder AddFontSrc(params string[] sources) => Add("font-src", sources);

    /// <summary>Appends source expressions to the <c>img-src</c> directive.</summary>
    public CspBuilder AddImgSrc(params string[] sources) => Add("img-src", sources);

    /// <summary>Appends source expressions to the <c>connect-src</c> directive.</summary>
    public CspBuilder AddConnectSrc(params string[] sources) => Add("connect-src", sources);

    /// <summary>Appends source expressions to the <c>frame-src</c> directive.</summary>
    public CspBuilder AddFrameSrc(params string[] sources) => Add("frame-src", sources);

    /// <summary>Appends source expressions to the <c>worker-src</c> directive.</summary>
    public CspBuilder AddWorkerSrc(params string[] sources) => Add("worker-src", sources);

    /// <summary>Appends source expressions to the <c>media-src</c> directive.</summary>
    public CspBuilder AddMediaSrc(params string[] sources) => Add("media-src", sources);

    /// <summary>Appends source expressions to the <c>object-src</c> directive.</summary>
    public CspBuilder AddObjectSrc(params string[] sources) => Add("object-src", sources);

    /// <summary>Appends source expressions to the <c>manifest-src</c> directive.</summary>
    public CspBuilder AddManifestSrc(params string[] sources) => Add("manifest-src", sources);

    /// <summary>Appends source expressions to the <c>child-src</c> directive.</summary>
    public CspBuilder AddChildSrc(params string[] sources) => Add("child-src", sources);

    // (prefetch-src dropped — deprecated in CSP L3.)
    // (sandbox deferred — token-based semantics, separate PR.)

    // --- Document directives ----------------------------------------------

    /// <summary>Appends source expressions to the <c>base-uri</c> directive.</summary>
    public CspBuilder AddBaseUri(params string[] sources) => Add("base-uri", sources);

    // --- Navigation directives --------------------------------------------

    /// <summary>Appends source expressions to the <c>form-action</c> directive.</summary>
    public CspBuilder AddFormAction(params string[] sources) => Add("form-action", sources);

    /// <summary>Appends source expressions to the <c>frame-ancestors</c> directive.</summary>
    public CspBuilder AddFrameAncestors(params string[] sources) => Add("frame-ancestors", sources);

    // --- Reporting & misc -------------------------------------------------

    /// <summary>
    /// Sets the <c>report-uri</c> endpoint for CSP violation reports. Overwrites any
    /// previous value (the directive takes a single URI in CSP L2/L3).
    /// </summary>
    public CspBuilder SetReportUri(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);
        ValidateSource(uri);
        _reportUri = uri;
        return this;
    }

    /// <summary>
    /// Sets the <c>report-to</c> reporting group name (Reporting API). Overwrites any
    /// previous value.
    /// </summary>
    public CspBuilder SetReportTo(string groupName)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupName);
        ValidateSource(groupName);
        _reportTo = groupName;
        return this;
    }

    /// <summary>
    /// Enables (or disables) the <c>upgrade-insecure-requests</c> directive.
    /// </summary>
    public CspBuilder SetUpgradeInsecureRequests(bool enabled = true)
    {
        _upgradeInsecureRequests = enabled;
        return this;
    }

    // --- Composer-facing read surface (public, no IVT) -------------------

    /// <summary>
    /// Snapshot of all directives with at least one source, keyed by
    /// directive name (<c>"script-src"</c>, ...). The composer reads this,
    /// applies the <c>'none'</c>-drop rule, and serialises.
    /// </summary>
    /// <remarks>
    /// Each access produces a fresh snapshot; subsequent mutations to the
    /// builder are not visible in a previously-read snapshot. Deep-copied
    /// — both the key set and each source set are independent of the
    /// builder's internal state.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Directives =>
        new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
            _directives.ToDictionary(
                static kv => kv.Key,
                static kv => (IReadOnlyCollection<string>)kv.Value.ToArray(),
                StringComparer.Ordinal));

    /// <summary>The <c>report-uri</c> endpoint, or <c>null</c> if not set.</summary>
    public string? ReportUri => _reportUri;

    /// <summary>The <c>report-to</c> group name, or <c>null</c> if not set.</summary>
    public string? ReportTo => _reportTo;

    /// <summary>Whether the <c>upgrade-insecure-requests</c> directive should be emitted.</summary>
    public bool UpgradeInsecureRequests => _upgradeInsecureRequests;

    // --- Internals --------------------------------------------------------

    private CspBuilder Add(string directive, string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Length == 0)
        {
            return this;
        }

        if (!_directives.TryGetValue(directive, out HashSet<string>? set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _directives[directive] = set;
        }

        foreach (string source in sources)
        {
            ArgumentException.ThrowIfNullOrEmpty(source);
            ValidateSource(source);
            set.Add(source);
        }

        return this;
    }

    private static void ValidateSource(string source)
    {
        // Reject directive separators, CR/LF, NUL, and all C0 controls + DEL.
        // These would either break the CSP header (newline → header injection)
        // or silently produce a malformed policy that browsers ignore.
        if (source.Any(c => c == ';' || c == '\r' || c == '\n' || c < 0x20 || c == 0x7F))
        {
            throw new ArgumentException(
                "Invalid CSP source: directive separators (';') and control " +
                "characters (CR, LF, NUL, C0, DEL) are not allowed. " +
                $"Got source '{Sanitise(source)}'.",
                nameof(source));
        }
    }

    private static string Sanitise(string s)
    {
        // For the exception message only — replace non-printables so the
        // logged source is readable without leaking control bytes into logs.
        char[] buf = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            buf[i] = c < 0x20 || c == 0x7F ? '?' : c;
        }
        return new string(buf);
    }
}
