namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Selectable facets of the per-entity manifest. Wire form: comma-separated kebab-case
/// names in the <c>?facets=</c> query parameter (e.g. <c>?facets=identity,form</c>).
/// Default — when no value is supplied — returns every facet.
/// </summary>
[Flags]
public enum EntityFacets
{
    /// <summary>No facet selected (sentinel — never sent on the wire).</summary>
    None = 0,

    /// <summary>Wire identifier, display key, icon, permission group, schema version.</summary>
    Identity = 1 << 0,

    /// <summary>Boolean flags computed from the user's granted permissions (<c>canRead</c>, <c>canCreate</c>, …).</summary>
    Permissions = 1 << 1,

    /// <summary>Form variants — sections + fields + visibility rules.</summary>
    Forms = 1 << 2,

    /// <summary>Detail-view variants — sections + side panels.</summary>
    Details = 1 << 3,

    /// <summary>List/kanban collection metadata — query references and default view resolution.</summary>
    Collections = 1 << 4,

    /// <summary>Dashboards embedded on the detail header.</summary>
    Dashboards = 1 << 5,

    /// <summary>CSV / XLSX export references.</summary>
    Exports = 1 << 6,

    /// <summary>EntityView references owned by or shared with the requesting user (per ADR-047).</summary>
    Views = 1 << 7,

    /// <summary>All facets — the default when <c>?facets=</c> is absent.</summary>
    All = Identity | Permissions | Forms | Details | Collections | Dashboards | Exports | Views,
}
