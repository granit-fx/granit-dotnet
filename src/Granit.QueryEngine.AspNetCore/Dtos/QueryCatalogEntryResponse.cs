namespace Granit.QueryEngine.AspNetCore.Dtos;

/// <summary>
/// Response payload for a single entry in <c>GET /catalog</c>. Mirrors the wire-relevant
/// subset of <see cref="IQueryDefinitionDescriptor"/>, letting a dashboard editor offer a
/// dropdown of registered queries instead of a free-text <c>queryName</c>.
/// </summary>
/// <param name="Name">
/// Wire identifier of the query definition — e.g. <c>"Acme.Patients"</c>. Stable across
/// processes; safe to persist as the selected query in a dashboard widget.
/// </param>
/// <param name="BasePath">
/// Resolved HTTP base path of the query's list endpoint (e.g. <c>"/api/patients"</c>), or
/// <c>null</c> when the definition is registered but no <c>MapGranitQuery</c> route exposes
/// it. Registration and routing are decoupled, so a forged URL is never emitted — the
/// frontend surfaces a query without a base path as not-yet-routable.
/// </param>
/// <param name="Label">
/// Human-facing label for the dropdown. The descriptor carries no display metadata, so this
/// degrades gracefully to <see cref="Name"/>.
/// </param>
public sealed record QueryCatalogEntryResponse(
    string Name,
    string? BasePath,
    string Label);
