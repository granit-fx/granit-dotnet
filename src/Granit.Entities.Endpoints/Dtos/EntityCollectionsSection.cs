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
/// front-end component selection; per-kind config lives in <see cref="Kanban"/>,
/// <see cref="Calendar"/>, or <see cref="Gallery"/> (and a future <c>Map</c>
/// sub-record).
/// </summary>
/// <param name="Kind">Layout kind from the closed catalog.</param>
/// <param name="IsDefault">Whether this layout is the default tab on first render.</param>
/// <param name="Kanban">Kanban-specific configuration when <see cref="Kind"/> is <see cref="EntityListLayoutKind.Kanban"/>.</param>
/// <param name="Calendar">Calendar-specific configuration when <see cref="Kind"/> is <see cref="EntityListLayoutKind.Calendar"/>.</param>
/// <param name="Gallery">Gallery-specific configuration when <see cref="Kind"/> is <see cref="EntityListLayoutKind.Gallery"/>.</param>
public sealed record EntityListLayoutManifest(
    EntityListLayoutKind Kind,
    bool IsDefault,
    EntityKanbanLayoutManifest? Kanban,
    EntityCalendarLayoutManifest? Calendar,
    EntityGalleryLayoutManifest? Gallery);

/// <summary>
/// Calendar-specific layout configuration carried in the manifest. Property
/// names address fields on the entity; the renderer (<c>EntityCalendar</c>)
/// reads the actual values via the range-query endpoint
/// (<c>GET /api/entities/{name}/calendar</c>).
/// </summary>
/// <param name="StartPropertyName">Entity property carrying the event start (required).</param>
/// <param name="EndPropertyName">Entity property carrying the event end. <see langword="null"/> for point-in-time markers.</param>
/// <param name="TitlePropertyName">Entity property used as the event headline, or <see langword="null"/> for the entity's <c>DisplayProperty</c> fallback.</param>
/// <param name="ColorByPropertyName">Entity property used to bucket events into colour groups, or <see langword="null"/> for theme default.</param>
public sealed record EntityCalendarLayoutManifest(
    string StartPropertyName,
    string? EndPropertyName,
    string? TitlePropertyName,
    string? ColorByPropertyName);

/// <summary>
/// Gallery-specific layout configuration carried in the manifest. Property
/// names address fields on the entity; the renderer (<c>EntityGallery</c>)
/// reads each row's image via the host's blob-storage download endpoint and
/// labels the card with <see cref="TitlePropertyName"/> /
/// <see cref="SubtitlePropertyName"/>.
/// </summary>
/// <param name="ImagePropertyName">Entity property carrying the card image — typed <c>BlobReference</c> (or nullable). Required.</param>
/// <param name="TitlePropertyName">Entity property used as the card headline, or <see langword="null"/> for the entity's <c>DisplayProperty</c> fallback.</param>
/// <param name="SubtitlePropertyName">Optional secondary line under the title, or <see langword="null"/> for none.</param>
/// <param name="CardSize">Card size — drives CSS-grid track sizing in the renderer.</param>
public sealed record EntityGalleryLayoutManifest(
    string ImagePropertyName,
    string? TitlePropertyName,
    string? SubtitlePropertyName,
    GalleryCardSize CardSize);

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
/// an ordered list of body fields, plus pinned smart-buttons (relations) and
/// pinned icon-buttons (actions) the contributors opted to surface on the tile.
/// </summary>
/// <param name="TitleProperty">Entity property used as the tile headline, or <see langword="null"/> for the entity's <c>DisplayProperty</c> fallback.</param>
/// <param name="Fields">Body fields, in declaration order — already permission-filtered server-side.</param>
/// <param name="Relations">Compact references to the entity's relations that opted into kanban via <c>Relation.OnKanbanCard()</c>. Already permission-filtered.</param>
/// <param name="Actions">Compact references to the entity's actions that opted into kanban via <c>Action.OnKanbanCard()</c>. Already permission-filtered.</param>
public sealed record EntityKanbanCardManifest(
    string? TitleProperty,
    IReadOnlyList<EntityFormFieldManifest> Fields,
    IReadOnlyList<EntityKanbanCardRelationManifest> Relations,
    IReadOnlyList<EntityKanbanCardActionManifest> Actions);

/// <summary>
/// Compact reference to one relation pinned on a kanban tile. Carries only the
/// fields the renderer needs to draw a small smart-button (name, label, icon,
/// permission-aware aggregate selection) — the full relation descriptor stays
/// addressable via the entity's <c>Relations</c> facet.
/// </summary>
/// <param name="Name">Stable relation name — matches the entry in the entity's <c>Relations</c> facet.</param>
/// <param name="DisplayKey">i18n key for the user-facing label.</param>
/// <param name="Icon">Icon override on the smart-button.</param>
/// <param name="ContributorAssemblyName">Contributing assembly. <see langword="null"/> for intra-module declarations.</param>
public sealed record EntityKanbanCardRelationManifest(
    string Name,
    string? DisplayKey,
    string? Icon,
    string? ContributorAssemblyName);

/// <summary>
/// Compact reference to one action pinned on a kanban tile. Same wire shape
/// philosophy as <see cref="EntityKanbanCardRelationManifest"/> — the renderer
/// looks up the full descriptor (URL template, HTTP method, confirmation key)
/// in the entity's <c>Actions</c> facet via <see cref="Name"/>.
/// </summary>
/// <param name="Name">Stable action name — matches the entry in the entity's <c>Actions</c> facet.</param>
/// <param name="DisplayKey">i18n key for the user-facing label (rendered as tooltip on the icon-button).</param>
/// <param name="Icon">Icon name from the catalog.</param>
/// <param name="ContributorAssemblyName">Contributing assembly. <see langword="null"/> for intra-module declarations.</param>
public sealed record EntityKanbanCardActionManifest(
    string Name,
    string? DisplayKey,
    string? Icon,
    string? ContributorAssemblyName);

/// <summary>One per-value kanban column declaration.</summary>
/// <param name="Value">Wire form of the discrete <c>GroupBy</c> value (enum member name, string literal, …).</param>
/// <param name="Color">Optional column colour from the closed catalog, or <see langword="null"/> for theme default.</param>
/// <param name="DefaultState">Open / Collapsed / Hidden — initial render state per ADR-040.</param>
public sealed record EntityKanbanColumnManifest(
    string Value,
    KanbanColor? Color,
    KanbanColumnState DefaultState);
