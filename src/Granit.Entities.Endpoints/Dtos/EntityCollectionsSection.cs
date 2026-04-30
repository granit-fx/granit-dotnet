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
public sealed record EntityCollectionsSection(
    EntityCollectionReference? Query,
    EntityCollectionReference? Export,
    IReadOnlyList<EntityCollectionReference> Metrics,
    IReadOnlyList<EntityCollectionReference> Dashboards,
    Guid? DefaultViewId);

/// <summary>
/// Reference to one external declarative primitive (Query / Export / Metric / Dashboard
/// / Workflow).
/// </summary>
/// <param name="Name">Wire identifier (e.g. <c>"Granit.Invoicing.InvoiceQuery"</c>).</param>
/// <param name="ClrTypeName">Short CLR type name of the underlying definition (debugging aid).</param>
public sealed record EntityCollectionReference(
    string Name,
    string ClrTypeName);
