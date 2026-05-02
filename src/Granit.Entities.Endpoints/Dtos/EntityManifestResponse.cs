namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Per-entity manifest payload returned by <c>GET /api/entities/{name}</c>. Every
/// section is optional in the wire form — when the caller passes <c>?facets=</c>
/// to slim the response, the omitted sections are <see langword="null"/>.
/// </summary>
/// <param name="SchemaVersion">Manifest schema version (semver-major). Bumps on breaking shape changes; mirrors the <c>Granit-Entities-Schema-Version</c> header.</param>
/// <param name="Identity">Identity facet — wire id, displayKey, icon, …</param>
/// <param name="Permissions">Permission flags computed for the requesting user.</param>
/// <param name="Forms">Form variants the user is allowed to see (already permission-filtered).</param>
/// <param name="Details">Detail-view variants the user is allowed to see (already permission-filtered).</param>
/// <param name="Collections">Query / Export / Metric / Dashboard references + resolved default view.</param>
/// <param name="Relations">Relations surfaced on the source entity (Tab / SmartButton / Sidebar / InlineChips per ADR-048). Already permission-filtered server-side.</param>
/// <param name="Actions">Actions exposed on the entity (intra-module + cross-module contributions). Already permission-filtered server-side.</param>
/// <param name="Activities">Activities opt-in section (ADR-046). Present only when the entity declared <c>.Activities()</c> AND the host loaded the <c>Granit.Activities</c> runtime.</param>
public sealed record EntityManifestResponse(
    int SchemaVersion,
    EntityIdentitySection? Identity,
    EntityPermissionsSection? Permissions,
    IReadOnlyList<EntityFormManifest>? Forms,
    IReadOnlyList<EntityDetailManifest>? Details,
    EntityCollectionsSection? Collections,
    IReadOnlyList<EntityRelationManifest>? Relations,
    IReadOnlyList<EntityActionManifest>? Actions = null,
    EntityActivitiesManifest? Activities = null);
