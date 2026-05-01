using Granit.Entities.Layouts;

namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Collections facet — lists the wire identifiers of the queries / exports / dashboards
/// surfaced by this entity. The full <c>QueryMetadata</c> payload remains served by the
/// existing <c>GET /api/.../meta</c> endpoint per query (no double-rendering here);
/// the manifest only carries the keys + URLs the frontend uses to address them.
/// </summary>
/// <param name="Query">Query reference (list / kanban), or <see langword="null"/> when the entity has no list collection.</param>
/// <param name="Export">Export reference (CSV / XLSX), or <see langword="null"/>.</param>
/// <param name="Metrics">Metric KPIs surfaced on the detail header.</param>
/// <param name="Dashboards">Dashboards embedded on the detail header.</param>
/// <param name="DefaultViewId">Resolved default <c>EntityView</c> id per ADR-049's 5-tier resolver, or <see langword="null"/> when none applies.</param>
/// <param name="ListLayouts">Alternative list-view layouts (kanban, …) the entity exposes — drives the EntityListViewSwitcher tabs.</param>
public sealed record EntityCollectionsSection(
    EntityCollectionReference? Query,
    EntityCollectionReference? Export,
    IReadOnlyList<EntityCollectionReference> Metrics,
    IReadOnlyList<EntityCollectionReference> Dashboards,
    Guid? DefaultViewId,
    IReadOnlyList<EntityListLayoutManifest> ListLayouts);

/// <summary>
/// Reference to one external declarative primitive (Query / Export / Metric / Dashboard
/// / Workflow).
/// </summary>
/// <param name="Name">Wire identifier (e.g. <c>"Granit.Invoicing.InvoiceQuery"</c>).</param>
/// <param name="ClrTypeName">Short CLR type name of the underlying definition (debugging aid).</param>
public sealed record EntityCollectionReference(
    string Name,
    string ClrTypeName);

/// <summary>
/// One alternative list-view layout exposed in the manifest. The kind drives
/// front-end component selection; per-kind config lives in <see cref="Kanban"/>
/// (and future <c>Calendar</c> / <c>Map</c> / <c>Gallery</c> sub-records).
/// </summary>
/// <param name="Kind">Layout kind from the closed catalog.</param>
/// <param name="IsDefault">Whether this layout is the default tab on first render.</param>
/// <param name="Kanban">Kanban-specific configuration when <see cref="Kind"/> is <see cref="EntityListLayoutKind.Kanban"/>.</param>
public sealed record EntityListLayoutManifest(
    EntityListLayoutKind Kind,
    bool IsDefault,
    EntityKanbanLayoutManifest? Kanban);

/// <summary>Kanban-specific layout configuration carried in the manifest.</summary>
/// <param name="GroupByPropertyName">Entity property used to bucket rows into columns.</param>
/// <param name="GroupByClrTypeName">Short CLR type name of the group-by property — drives front-end value-parsing.</param>
/// <param name="Card">Card-content schema for each tile.</param>
/// <param name="Columns">Per-value column metadata (colour + default state).</param>
public sealed record EntityKanbanLayoutManifest(
    string GroupByPropertyName,
    string GroupByClrTypeName,
    EntityKanbanCardManifest Card,
    IReadOnlyList<EntityKanbanColumnManifest> Columns);

/// <summary>
/// Card-content schema rendered inside a kanban tile. Frappe-style: optional
/// title (falls back to the entity's <c>DisplayProperty</c> when absent) plus
/// an ordered list of body fields.
/// </summary>
/// <param name="TitleProperty">Entity property used as the tile headline, or <see langword="null"/> for the entity's <c>DisplayProperty</c> fallback.</param>
/// <param name="Fields">Body fields, in declaration order — already permission-filtered server-side.</param>
public sealed record EntityKanbanCardManifest(
    string? TitleProperty,
    IReadOnlyList<EntityFormFieldManifest> Fields);

/// <summary>One per-value kanban column declaration.</summary>
/// <param name="Value">Wire form of the discrete <c>GroupBy</c> value (enum member name, string literal, …).</param>
/// <param name="Color">Optional column colour from the closed catalog, or <see langword="null"/> for theme default.</param>
/// <param name="DefaultState">Open / Collapsed / Hidden — initial render state per ADR-040.</param>
public sealed record EntityKanbanColumnManifest(
    string Value,
    KanbanColor? Color,
    KanbanColumnState DefaultState);
