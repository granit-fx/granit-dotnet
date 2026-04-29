using System.Text.Json;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Wire-shape for <c>POST /dashboards/{id}/render</c>. Bundles every widget's
/// envelope into one HTTP round-trip. Per ADR-039 §6 — the bundle is a
/// transport optimisation, not the cache identity (the frontend's
/// <c>useDashboard</c> hook splits it into per-widget TanStack entries before
/// storing it).
/// </summary>
/// <param name="DashboardId">Dashboard identifier — matches <c>Dashboard.Id</c>.</param>
/// <param name="RenderedAt">Server-side timestamp at which the bundle was composed.</param>
/// <param name="Period">Period the renderer ran against. <see langword="null"/> when the request omitted period bounds.</param>
/// <param name="Widgets">One flat record per widget, in <c>WidgetInstance.Position</c> order.</param>
public sealed record DashboardRenderResponse(
    Guid DashboardId,
    DateTimeOffset RenderedAt,
    DashboardRenderPeriodResponse? Period,
    IReadOnlyList<DashboardRenderedWidgetResponse> Widgets);

/// <summary>
/// Period echoed back to the client. <see cref="Token"/> mirrors the request's
/// named-token field unchanged — useful for clients that compose UIs around
/// rotating presets (<c>"mtd"</c>, <c>"qtd"</c>, …).
/// </summary>
/// <param name="From">Inclusive lower bound (UTC).</param>
/// <param name="To">Exclusive upper bound (UTC).</param>
/// <param name="Token">Optional named token echoed from the request.</param>
public sealed record DashboardRenderPeriodResponse(DateTimeOffset From, DateTimeOffset To, string? Token = null);

/// <summary>
/// One widget's slot in the bundle. Flattens the widget id alongside the
/// structural metadata of the persisted <c>WidgetInstance</c>, the declarative
/// click-handlers from the source <c>WidgetDefinition</c>, and the envelope
/// fields produced by <see cref="WidgetSnapshotEnvelope"/>. The wire shape
/// ADR-039 §6 locks for every framework dashboard endpoint — both the bundle
/// path (<c>POST /dashboards/{id}/render</c>) and the per-widget single-render
/// path (<c>POST /widgets/{kind}/render</c>) project to this same record so
/// frontend renderers can be wrapped identically across the two paths.
/// </summary>
/// <param name="Id">Persisted <c>WidgetInstance.Id</c>.</param>
/// <param name="WidgetType">Declarative kind discriminator — mirrors <c>WidgetDefinition</c>'s <c>[JsonDerivedType]</c> tag (<c>"Kpi"</c>, <c>"Markdown"</c>, …).</param>
/// <param name="Slug">Per-widget slug, extracted from <see cref="TitleLocalizationKey"/> (<c>"Widget:{DashboardName}.{Slug}"</c>). Surfaced flat so the frontend's action dispatcher can address the widget without re-parsing the key.</param>
/// <param name="Position">0-based dense-ranked grid position; matches <c>WidgetInstance.Position</c>.</param>
/// <param name="Width">Grid columns spanned by the widget; matches <c>WidgetInstance.Width</c>.</param>
/// <param name="Height">Grid rows spanned by the widget; matches <c>WidgetInstance.Height</c>.</param>
/// <param name="TitleLocalizationKey">Localization key for the widget title (<c>Widget:{DashboardName}.{Slug}</c>); the frontend resolves it client-side.</param>
/// <param name="Actions">Declarative click-handlers from the source <c>WidgetDefinition</c>. <see langword="null"/> when the widget declared none — the frontend dispatcher ignores it.</param>
/// <param name="RequiredPermission">Optional per-widget permission gate; mirrors <c>WidgetInstance.RequiredPermission</c>. Already enforced server-side (lacking the permission yields <see cref="WidgetSnapshotStatus.Unavailable"/>); echoed here for the frontend's defensive UI.</param>
/// <param name="Status">Runtime outcome (<see cref="WidgetSnapshotStatus.Snapshot"/> / <see cref="WidgetSnapshotStatus.Unavailable"/> / <see cref="WidgetSnapshotStatus.Error"/>).</param>
/// <param name="Sequence">Always <c>1</c> in pull mode; future push transport increments per (widget, tenant). EPIC #1366 invariant #2.</param>
/// <param name="EmittedAt">Server-side timestamp of the widget's computation.</param>
/// <param name="RefreshHint">Pull / push transport hint — drives the frontend's per-widget cache TTL.</param>
/// <param name="Snapshot">Pre-serialised typed payload. <see langword="null"/> when <see cref="Status"/> is not <see cref="WidgetSnapshotStatus.Snapshot"/>.</param>
/// <param name="ReasonLocalizationKey">Localization key for the user-facing reason — set on <see cref="WidgetSnapshotStatus.Unavailable"/> and <see cref="WidgetSnapshotStatus.Error"/>; <see langword="null"/> on <see cref="WidgetSnapshotStatus.Snapshot"/>. Resolved client-side so the same envelope can be cached across user locales.</param>
public sealed record DashboardRenderedWidgetResponse(
    Guid Id,
    string WidgetType,
    string Slug,
    int Position,
    int Width,
    int Height,
    string TitleLocalizationKey,
    IReadOnlyList<WidgetAction>? Actions,
    string? RequiredPermission,
    WidgetSnapshotStatus Status,
    long Sequence,
    DateTimeOffset EmittedAt,
    RefreshHint RefreshHint,
    JsonElement? Snapshot,
    string? ReasonLocalizationKey = null);
