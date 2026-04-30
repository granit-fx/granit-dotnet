namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// One entity entry in the <c>GET /api/entities</c> discovery tree. Entities the
/// caller cannot read are omitted entirely (defense-in-depth, ADR-040 §6 / story #1549) —
/// not just hidden via a flag.
/// </summary>
/// <param name="Name">Wire identifier (e.g. <c>"Granit.Parties.Party"</c>).</param>
/// <param name="DisplayKey">i18n key for the user-facing display name (singular).</param>
/// <param name="Icon">Icon name from the standard catalog, or <see langword="null"/> if none.</param>
/// <param name="PermissionGroup">Permission-group prefix (e.g. <c>"Parties.Parties"</c>) — surfaced for the admin UI.</param>
/// <param name="Links">Hypermedia links — <c>manifest</c>, <c>list</c> (when a query is registered), <c>defaultView</c> (when an EntityView default is configured).</param>
public sealed record EntityDiscoveryItemResponse(
    string Name,
    string? DisplayKey,
    string? Icon,
    string? PermissionGroup,
    EntityDiscoveryLinks Links);

/// <summary>Hypermedia links attached to a discovery entry.</summary>
/// <param name="Manifest">Absolute path to the per-entity manifest (always present).</param>
/// <param name="List">Absolute path to the list query endpoint, or <see langword="null"/> when no <c>QueryDefinition</c> is registered.</param>
public sealed record EntityDiscoveryLinks(
    string Manifest,
    string? List);

/// <summary>One module group in the discovery tree.</summary>
/// <param name="Module">Module name (PascalCase, derived from the entity's namespace prefix — e.g. <c>"Parties"</c>).</param>
/// <param name="Items">Entities registered under this module that the caller can read.</param>
public sealed record EntityModuleGroupResponse(
    string Module,
    IReadOnlyList<EntityDiscoveryItemResponse> Items);

/// <summary>Discovery tree root.</summary>
/// <param name="SchemaVersion">Manifest schema version (semver-major). Bumps on breaking shape changes.</param>
/// <param name="Modules">Module groups, alphabetical by <see cref="EntityModuleGroupResponse.Module"/>.</param>
public sealed record EntityDiscoveryResponse(
    int SchemaVersion,
    IReadOnlyList<EntityModuleGroupResponse> Modules);
