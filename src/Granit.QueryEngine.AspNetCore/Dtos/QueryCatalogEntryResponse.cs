namespace Granit.QueryEngine.AspNetCore.Dtos;

/// <summary>
/// Response payload for a single entry in <c>GET /catalog</c>. Mirrors the wire-relevant
/// subset of <see cref="IQueryDefinitionDescriptor"/>, letting a dashboard editor offer a
/// dropdown of registered queries instead of a free-text <c>queryName</c>.
/// </summary>
/// <param name="ModuleName">
/// Owning module of the query — e.g. <c>"Auditing"</c> for both <c>AuditEntryQuery</c> and
/// <c>AuditEntityChangeQuery</c>. The catalogue is ordered by module, then by name, so a
/// dashboard editor can present queries grouped under their module heading.
/// </param>
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
/// <param name="LabelKey">
/// Localization key the frontend resolves for the dropdown label, in the same way it resolves
/// entity display names. It is the target entity's <c>DisplayKey</c> (e.g. <c>"Entity:Party"</c>)
/// when the query targets a registered entity — reusing the already-translated entity name — and
/// otherwise <c>"Query:{Name}"</c>, a key a module may declare in its own localization resource.
/// The server emits a key, never a resolved string: translation happens client-side against the
/// merged i18n bundle, consistent with entity discovery, permissions and validation.
/// </param>
public sealed record QueryCatalogEntryResponse(
    string ModuleName,
    string Name,
    string? BasePath,
    string LabelKey);
