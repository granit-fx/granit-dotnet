namespace Granit.Privacy.Endpoints.Discovery;

/// <summary>
/// Configuration for the Global Privacy Control (GPC) public discovery resource
/// served at <c>/.well-known/gpc.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Publishing this resource is a public, opposable declaration that the operator
/// honors GPC signals. Recognized by US state privacy laws including CPRA
/// (Cal. Civ. Code §1798.135(b)(1)), Colorado CPA, and Connecticut CTDPA.
/// </para>
/// <para>
/// Opt-in only — disabled by default. When disabled, no route is mapped and the
/// path returns 404 naturally. When enabled, <see cref="LastUpdate"/> MUST be set
/// (validated at startup via <see cref="GpcDiscoveryOptionsValidator"/>).
/// </para>
/// <para>
/// <b>Host scoping.</b> The <c>.well-known</c> URI scheme (RFC 8615) is host-rooted:
/// publishing the document from this endpoint covers the host that responds to it
/// (typically the BFF). When the front-end is served from a distinct host, that
/// host MUST publish its own copy (e.g. a static <c>public/.well-known/gpc.json</c>
/// in the React/Vite/Next app), or a reverse proxy must route both hosts'
/// <c>.well-known/*</c> to this endpoint.
/// </para>
/// <para>
/// Spec: <see href="https://w3c.github.io/gpc/#the-well-known-resource"/>.
/// </para>
/// </remarks>
public sealed class GpcDiscoveryOptions
{
    /// <summary>
    /// Configuration section name bound by <c>BindConfiguration</c>.
    /// </summary>
    public const string SectionName = "Privacy:GpcDiscovery";

    /// <summary>
    /// When <see langword="true"/>, maps <c>GET /.well-known/gpc.json</c> serving
    /// the GPC discovery document. Default: <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Date at which the operator made the statement of support for GPC, formatted
    /// per RFC 3339 full-date (<c>YYYY-MM-DD</c>). Required when <see cref="Enabled"/>
    /// is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Per the GPC spec this value is a <b>legal anchor</b>: it pins the interpretation
    /// of the declaration to the version of the GPC standard in force at this date,
    /// so later changes to the standard do not retroactively alter what the operator
    /// committed to. Set it deliberately and only bump it when materially restating
    /// the commitment (broadened scope, revised privacy policy, alignment with a new
    /// GPC version). Do <b>not</b> auto-default it to today's date or the build date —
    /// that would silently move the legal anchor on every deploy and defeat its purpose.
    /// </remarks>
    public DateOnly? LastUpdate { get; set; }

    /// <summary>
    /// <c>Cache-Control max-age</c> (seconds) emitted with the discovery document.
    /// Default: 86400 (1 day) — matches the day-level granularity of
    /// <see cref="LastUpdate"/>. Must be &gt;= 0.
    /// </summary>
    public int CacheMaxAgeSeconds { get; set; } = 86_400;
}
