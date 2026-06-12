using System.Collections.Concurrent;
using System.Text;
using Granit.Http.SecurityHeaders.Csp;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Internal;

/// <summary>
/// Builds the per-endpoint Content-Security-Policy header value from the base
/// <see cref="CspOptions"/> and every registered <see cref="ICspContributor"/>
/// that opts in for the request's matched endpoint. Caches per
/// <see cref="Endpoint"/> and invalidates the cache when
/// <see cref="GranitSecurityHeadersOptions"/> changes.
/// </summary>
internal sealed partial class CspComposer : IDisposable
{
    private readonly IOptionsMonitor<GranitSecurityHeadersOptions> _optionsMonitor;
    private readonly ICspContributorRegistry _registry;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CspComposer> _logger;

    private readonly ConcurrentDictionary<Endpoint, (string Name, string Value)> _cache = new();
    private readonly ConcurrentDictionary<string, byte> _loggedProdUnsafeInline = new(StringComparer.Ordinal);
    private readonly IDisposable? _onChangeSubscription;

    public CspComposer(
        IOptionsMonitor<GranitSecurityHeadersOptions> optionsMonitor,
        ICspContributorRegistry registry,
        IHostEnvironment environment,
        ILogger<CspComposer> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _optionsMonitor = optionsMonitor;
        _registry = registry;
        _environment = environment;
        _logger = logger;

        _onChangeSubscription = optionsMonitor.OnChange((_, _) =>
        {
            _cache.Clear();
            _loggedProdUnsafeInline.Clear();
        });

        LogStartup(optionsMonitor.CurrentValue, registry);
    }

    public void Dispose() => _onChangeSubscription?.Dispose();

    /// <summary>
    /// Composes the CSP header for the current request, or returns
    /// <c>null</c> when CSP emission is disabled (e.g. composed value is
    /// empty).
    /// </summary>
    public (string Name, string Value)? Compose(HttpContext context)
    {
        GranitSecurityHeadersOptions opts = _optionsMonitor.CurrentValue;
        CspOptions csp = opts.Csp;

        if (!string.IsNullOrEmpty(csp.RawOverride))
        {
            return (HeaderName(csp.ReportOnly), csp.RawOverride);
        }

        Endpoint? endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            // No matched endpoint (e.g. middleware short-circuit before
            // routing) — apply the base-only CSP without caching the empty
            // key. Rare path; ok to recompute.
            string baseValue = Serialise(SeedBuilder(csp), csp, includeBuilderReporting: false);
            return string.IsNullOrEmpty(baseValue) ? null : (HeaderName(csp.ReportOnly), baseValue);
        }

        (string Name, string Value) composed = _cache.GetOrAdd(
            endpoint,
            static (ep, state) => state.self.ComposeForEndpoint(ep, state.context, state.opts),
            (self: this, context, opts));

        return string.IsNullOrEmpty(composed.Value) ? null : composed;
    }

    private (string Name, string Value) ComposeForEndpoint(
        Endpoint endpoint,
        HttpContext context,
        GranitSecurityHeadersOptions opts)
    {
        CspOptions csp = opts.Csp;
        CspBuilder builder = SeedBuilder(csp);

        HashSet<string>? disabled = opts.DisabledContributors is { Count: > 0 }
            ? new HashSet<string>(opts.DisabledContributors, StringComparer.Ordinal)
            : null;

        List<string>? activeNames = null;
        foreach (ICspContributor contributor in _registry.Contributors)
        {
            if (disabled?.Contains(contributor.Name) == true)
            {
                continue;
            }

            contributor.Contribute(context, builder);
            (activeNames ??= []).Add(contributor.Name);
        }

        DetectNonceUnsafeInlineCollision(builder, endpoint);
        DetectProductionUnsafeInline(builder, activeNames);

        string value = Serialise(builder, csp, includeBuilderReporting: true);
        return (HeaderName(csp.ReportOnly), value);
    }

    private static CspBuilder SeedBuilder(CspOptions csp)
    {
        CspBuilder b = new();
        SeedDirective(b.AddDefaultSrc, csp.DefaultSrc);
        SeedDirective(b.AddScriptSrc, csp.ScriptSrc);
        SeedDirective(b.AddScriptSrcElem, csp.ScriptSrcElem);
        SeedDirective(b.AddScriptSrcAttr, csp.ScriptSrcAttr);
        SeedDirective(b.AddStyleSrc, csp.StyleSrc);
        SeedDirective(b.AddStyleSrcElem, csp.StyleSrcElem);
        SeedDirective(b.AddStyleSrcAttr, csp.StyleSrcAttr);
        SeedDirective(b.AddFontSrc, csp.FontSrc);
        SeedDirective(b.AddImgSrc, csp.ImgSrc);
        SeedDirective(b.AddConnectSrc, csp.ConnectSrc);
        SeedDirective(b.AddFrameSrc, csp.FrameSrc);
        SeedDirective(b.AddWorkerSrc, csp.WorkerSrc);
        SeedDirective(b.AddMediaSrc, csp.MediaSrc);
        SeedDirective(b.AddObjectSrc, csp.ObjectSrc);
        SeedDirective(b.AddManifestSrc, csp.ManifestSrc);
        SeedDirective(b.AddChildSrc, csp.ChildSrc);
        SeedDirective(b.AddBaseUri, csp.BaseUri);
        SeedDirective(b.AddFormAction, csp.FormAction);
        SeedDirective(b.AddFrameAncestors, csp.FrameAncestors);

        if (!string.IsNullOrEmpty(csp.ReportUri))
        {
            b.SetReportUri(csp.ReportUri);
        }

        if (!string.IsNullOrEmpty(csp.ReportTo))
        {
            b.SetReportTo(csp.ReportTo);
        }

        if (csp.UpgradeInsecureRequests)
        {
            b.SetUpgradeInsecureRequests();
        }

        return b;
    }

    private static void SeedDirective(Func<string[], CspBuilder> add, IList<string> sources)
    {
        if (sources is { Count: > 0 })
        {
            add([.. sources]);
        }
    }

    private static string Serialise(CspBuilder builder, CspOptions csp, bool includeBuilderReporting)
    {
        StringBuilder sb = new();
        bool first = true;

        AppendDirectives(sb, builder, ref first);
        AppendReportingHints(sb, builder, csp, includeBuilderReporting, ref first);

        return sb.ToString();
    }

    private static void AppendDirectives(StringBuilder sb, CspBuilder builder, ref bool first)
    {
        foreach (string directive in CspDirectiveNames.All)
        {
            if (!builder.Directives.TryGetValue(directive, out IReadOnlyCollection<string>? sources)
                || sources.Count == 0)
            {
                continue;
            }

            // 'none' drop: per CSP spec, 'none' cannot coexist with real
            // sources in the same directive. If the set contains any real
            // source, drop 'none'.
            IEnumerable<string> effective = sources.Count > 1 && sources.Contains("'none'")
                ? sources.Where(static s => !string.Equals(s, "'none'", StringComparison.Ordinal))
                : sources;

            if (!first) { sb.Append("; "); }
            first = false;
            sb.Append(directive);
            foreach (string source in effective)
            {
                sb.Append(' ').Append(source);
            }
        }
    }

    private static void AppendReportingHints(
        StringBuilder sb, CspBuilder builder, CspOptions csp, bool includeBuilderReporting, ref bool first)
    {
        // Reporting hints — builder state takes precedence over options
        // (a contributor that sets ReportUri overrides the global default).
        string? reportUri = includeBuilderReporting ? builder.ReportUri ?? csp.ReportUri : csp.ReportUri;
        if (!string.IsNullOrEmpty(reportUri))
        {
            if (!first) { sb.Append("; "); }
            first = false;
            sb.Append("report-uri ").Append(reportUri);
        }

        string? reportTo = includeBuilderReporting ? builder.ReportTo ?? csp.ReportTo : csp.ReportTo;
        if (!string.IsNullOrEmpty(reportTo))
        {
            if (!first) { sb.Append("; "); }
            first = false;
            sb.Append("report-to ").Append(reportTo);
        }

        bool upgrade = includeBuilderReporting
            ? builder.UpgradeInsecureRequests || csp.UpgradeInsecureRequests
            : csp.UpgradeInsecureRequests;
        if (upgrade)
        {
            if (!first) { sb.Append("; "); }
            sb.Append("upgrade-insecure-requests");
        }
    }

    private static string HeaderName(bool reportOnly) =>
        reportOnly ? "Content-Security-Policy-Report-Only" : "Content-Security-Policy";

    private void DetectNonceUnsafeInlineCollision(CspBuilder builder, Endpoint endpoint)
    {
        foreach (KeyValuePair<string, IReadOnlyCollection<string>> directive in builder.Directives)
        {
            bool hasUnsafeInline = false;
            bool hasNonceOrHash = false;
            foreach (string source in directive.Value)
            {
                if (string.Equals(source, "'unsafe-inline'", StringComparison.Ordinal))
                {
                    hasUnsafeInline = true;
                }
                else if (source.StartsWith("'nonce-", StringComparison.Ordinal)
                         || source.StartsWith("'sha256-", StringComparison.Ordinal)
                         || source.StartsWith("'sha384-", StringComparison.Ordinal)
                         || source.StartsWith("'sha512-", StringComparison.Ordinal))
                {
                    hasNonceOrHash = true;
                }

                if (hasUnsafeInline && hasNonceOrHash)
                {
                    LogNonceUnsafeInlineCollision(endpoint.DisplayName ?? "(unnamed)", directive.Key);
                    return;
                }
            }
        }
    }

    private void DetectProductionUnsafeInline(CspBuilder builder, List<string>? activeNames)
    {
        if (!_environment.IsProduction() || activeNames is null)
        {
            return;
        }

        bool anyUnsafeInline = builder.Directives.Any(
            directive => directive.Value.Contains("'unsafe-inline'"));

        if (!anyUnsafeInline)
        {
            return;
        }

        // One warning per distinct contributor name observed in a composition
        // that emits 'unsafe-inline'. Best-effort attribution: we can't know
        // which contributor wrote the source, so we name all of them.
        // TryAdd doubles as the de-dup filter — the lazy Where keeps it one log per name.
        foreach (string name in activeNames.Where(name => _loggedProdUnsafeInline.TryAdd(name, 0)))
        {
            LogProductionUnsafeInline(name);
        }
    }

    private void LogStartup(GranitSecurityHeadersOptions opts, ICspContributorRegistry registry)
    {
        // Iterate the Contributors property triggers the registry lock — that's
        // intentional: by the time the composer is constructed, contributor
        // registration is finished.
        string contributors = registry.Contributors.Count == 0
            ? "(none)"
            : string.Join(", ", registry.Contributors.Select(c => c.Name));

        string baseDirectives = string.IsNullOrEmpty(opts.Csp.RawOverride)
            ? Serialise(SeedBuilder(opts.Csp), opts.Csp, includeBuilderReporting: false)
            : $"(RawOverride: {opts.Csp.RawOverride})";

        LogComposerReady(baseDirectives, contributors);
    }

    // --- LoggerMessage source-generated logs -----------------------------

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "CSP composer ready. Base directives: {Base}. Contributors: {Contributors}.")]
    private partial void LogComposerReady(string @base, string contributors);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Composed CSP for endpoint '{Endpoint}' has both 'unsafe-inline' and a nonce/hash source in directive '{Directive}'. " +
                  "Per CSP spec, the nonce/hash suppresses 'unsafe-inline' — the relaxation is silently dropped by the browser.")]
    private partial void LogNonceUnsafeInlineCollision(string endpoint, string directive);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "CSP contributor {ContributorName} relaxes the policy with 'unsafe-inline' in production. " +
                  "ASVS V14.4.7 considers this an XSS surface. Migrate to a nonce-/hash-based policy.")]
    private partial void LogProductionUnsafeInline(string contributorName);
}
