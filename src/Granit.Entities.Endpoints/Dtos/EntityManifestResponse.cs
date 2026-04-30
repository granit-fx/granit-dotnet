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
public sealed record EntityManifestResponse(
    int SchemaVersion,
    EntityIdentitySection? Identity,
    EntityPermissionsSection? Permissions,
    IReadOnlyList<EntityFormManifest>? Forms,
    IReadOnlyList<EntityDetailManifest>? Details,
    EntityCollectionsSection? Collections);
