using System.Text.Json.Serialization;
using Granit.Dashboards.Widgets;

namespace Granit.Dashboards;

/// <summary>
/// Base record for every widget shipped by a <see cref="DashboardDefinition"/>.
/// Concrete widget kinds add their kind-specific configuration via inheritance — see
/// <c>Granit.Dashboards.Abstractions</c> for presentation-only widgets, and per-domain
/// packages (e.g. <c>Granit.Analytics</c>, future <c>Granit.IoT.Dashboards</c>) for
/// data-bound widgets.
/// </summary>
/// <param name="Slug">
/// Widget-local identifier (PascalCase, unique within the dashboard). Used to compose
/// localization keys (<c>Widget:{DashboardName}.{Slug}</c>) and as the stable
/// identifier under reorder operations.
/// </param>
/// <param name="Position">Dense-ranked grid order — 0-based, contiguous within the dashboard.</param>
/// <param name="Size">Width / height in grid cells.</param>
/// <param name="RequiredPermission">
/// Optional override of the permission gating this widget at render time. When
/// <c>null</c>, the runtime resolves the effective permission from the underlying
/// data source (metric / query / IoT topic) — see ADR-038 §6.
/// Presentation-only widgets ignore this field.
/// </param>
/// <param name="TimeWindowOverride">
/// Optional override of the dashboard-wide <see cref="DashboardDefinition.DefaultTimeWindow"/>
/// for this widget specifically. Useful when (a) the widget is rendered standalone
/// outside a dashboard (e.g. a KPI tile above an invoice list — no surrounding
/// <c>DashboardContext</c>), or (b) the widget needs a different range than its
/// peers (e.g. a year-to-date KPI next to last-30-days widgets).
/// Presentation-only widgets ignore this field.
/// </param>
/// <param name="Actions">
/// Declarative click-handler descriptors. Each <see cref="WidgetAction"/> binds a
/// trigger (click / row-click / series-click / legend-click) to a typed dispatch kind
/// (navigate / open view / open dashboard / export / open detail) plus an optional
/// param map. The frontend dispatches them — no code injection, no expression
/// evaluation; values may reference variables resolved by <c>IVariableSubstituter</c>
/// (P3.3). <c>null</c> = the widget has no actions wired.
/// </param>
/// <remarks>
/// JSON polymorphism uses a stable <c>"type"</c> discriminator with short tags
/// (<c>"markdown"</c>, <c>"image"</c>, <c>"text"</c>, <c>"kpi"</c>, ...). The three
/// presentation-only kinds are registered here via <see cref="JsonDerivedTypeAttribute"/>;
/// data-bound kinds defined in downstream packages (<c>Granit.Analytics</c>,
/// <c>Granit.IoT.Dashboards</c>, ...) extend the chain at runtime through
/// <see cref="WidgetDefinitionPolymorphism.AddDerivedType{TWidget}"/>. Avoids leaking
/// CLR type names into the wire format.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MarkdownWidgetDefinition), "markdown")]
[JsonDerivedType(typeof(ImageWidgetDefinition), "image")]
[JsonDerivedType(typeof(TextWidgetDefinition), "text")]
public abstract record WidgetDefinition(
    string Slug,
    int Position,
    WidgetSize Size,
    string? RequiredPermission = null,
    DashboardTimeWindow? TimeWindowOverride = null,
    IReadOnlyList<WidgetAction>? Actions = null);
