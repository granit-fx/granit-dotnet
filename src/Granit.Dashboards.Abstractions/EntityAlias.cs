namespace Granit.Dashboards;

/// <summary>
/// Named entity binding declared by a <see cref="DashboardDefinition"/> and resolved
/// at render time by a registered <see cref="EntityAliasResolver"/>. Lets the same
/// dashboard render against different entities (a single Customer, a chosen Device,
/// the current Tenant, ...) without duplicating the definition. P2.3 of the
/// dashboards-architecture-proposals roadmap.
/// </summary>
/// <param name="Name">
/// Alias identifier referenced from data sources (e.g. <c>"currentCustomer"</c>,
/// <c>"selectedDevice"</c>). Camel-case, unique within the dashboard.
/// </param>
/// <param name="EntityType">
/// Logical type the alias resolves to (<c>"Customer"</c>, <c>"Device"</c>,
/// <c>"Invoice"</c>, ...). Used by the runtime to route to the right
/// <see cref="EntityAliasResolver"/> implementation when the resolver kind is
/// generic (e.g. <c>UserSelectionResolver</c> picks the lookup typed against
/// the entity type).
/// </param>
/// <param name="Resolver">
/// Resolution strategy — see <see cref="EntityAliasResolver"/>'s five derived kinds.
/// </param>
public sealed record EntityAlias(
    string Name,
    string EntityType,
    EntityAliasResolver Resolver);
