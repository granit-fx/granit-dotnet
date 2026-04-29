namespace Granit.Analytics.Endpoints.Dtos.Widgets;

/// <summary>
/// Per-render context shared by every <c>POST /widgets/{kind}/render</c> endpoint —
/// the period window, locale, and dashboard-level filter bindings the typed
/// renderer applies on top of the supplied <c>WidgetDefinition</c>.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the field set of <c>DashboardRenderRequest</c> (the bundle path) but
/// is nested under <c>Context</c> on the per-widget request envelopes — keeps
/// the wire shape symmetric across the two paths and lets a frontend hook
/// reuse the same context-builder for both. Aliases land here in a future
/// story (P3.3 variable substitution) — not in scope for v1.
/// </para>
/// <para>
/// Both period bounds must be supplied together; supplying only one yields a
/// 400 from the validator, same convention as the bundle path. The named
/// <see cref="PeriodToken"/> is echoed back unchanged on the response — the
/// renderer never re-resolves it server-side.
/// </para>
/// </remarks>
/// <param name="PeriodFrom">Inclusive lower bound (UTC). Required when <see cref="PeriodTo"/> is supplied.</param>
/// <param name="PeriodTo">Exclusive upper bound (UTC). Required when <see cref="PeriodFrom"/> is supplied.</param>
/// <param name="PeriodToken">Optional named token (e.g. <c>"mtd"</c>) — echoed in the response for client convenience.</param>
/// <param name="Locale">Active BCP-47 locale tag (e.g. <c>"en"</c>, <c>"fr-CA"</c>). Defaults to <c>"en"</c> when omitted.</param>
/// <param name="Filters">Dashboard-level filter bindings. <see langword="null"/> = no filters; the typed renderers apply their own defaults.</param>
public sealed record WidgetRenderContextRequest(
    DateTimeOffset? PeriodFrom = null,
    DateTimeOffset? PeriodTo = null,
    string? PeriodToken = null,
    string? Locale = null,
    IReadOnlyDictionary<string, string>? Filters = null);
