namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Request body for <c>POST /dashboards/{id}/render</c> — drives the per-render
/// <see cref="Rendering.WidgetRenderContext"/> the dashboard renderer hands to
/// each <see cref="Rendering.IWidgetInstanceRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The period window is supplied as absolute <see cref="PeriodFrom"/> /
/// <see cref="PeriodTo"/> bounds; named-token resolution (<c>"mtd"</c>,
/// <c>"qtd"</c>, …) is the caller's responsibility (mirrors the analytics
/// inline-metric endpoint, which resolves tokens client-side or via a separate
/// helper). Both bounds are required together — supplying only one is a 400.
/// </para>
/// <para>
/// <see cref="Filters"/> mirrors the dashboard-level filter spec:
/// <c>{ "Status": "Open" }</c> drives an unpaid-invoices KPI and an
/// unpaid-invoices table on the same dashboard. The renderer pipeline doesn't
/// interpret the values — each typed widget renderer applies them in its own
/// way.
/// </para>
/// </remarks>
/// <param name="PeriodFrom">Inclusive lower bound (UTC). Required when <see cref="PeriodTo"/> is supplied.</param>
/// <param name="PeriodTo">Exclusive upper bound (UTC). Required when <see cref="PeriodFrom"/> is supplied.</param>
/// <param name="PeriodToken">Optional named token (e.g. <c>"mtd"</c>) — echoed in the response for client convenience; the renderer never re-resolves it server-side.</param>
/// <param name="Locale">Active BCP-47 locale tag (e.g. <c>"en"</c>, <c>"fr-CA"</c>). Defaults to <c>"en"</c> when omitted.</param>
/// <param name="Filters">Dashboard-level filter bindings. <see langword="null"/> = no filters; the typed renderers apply their own defaults.</param>
/// <param name="ViewName">
/// Active <c>DashboardView.Name</c> the caller wants rendered. <see langword="null"/>
/// (default) falls back to <c>DashboardDefinition.DefaultView</c>, then to the first
/// declared view, then to the dashboard's top-level widget pool when the source
/// definition has no views — mirrors the frontend's <c>resolveActiveView</c> chain
/// (story P2.1) so the bundle path now honours view switches just like the
/// definition path. Single-view dashboards ignore this parameter; the response's
/// <c>ActiveViewName</c> echoes <see langword="null"/> in that case.
/// </param>
public sealed record DashboardRenderRequest(
    DateTimeOffset? PeriodFrom = null,
    DateTimeOffset? PeriodTo = null,
    string? PeriodToken = null,
    string? Locale = null,
    IReadOnlyDictionary<string, string>? Filters = null,
    string? ViewName = null);
