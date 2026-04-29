namespace Granit.Dashboards.Rendering;

/// <summary>
/// Resolved form of an <see cref="EntityAlias"/> at render time. Produced by the
/// dashboard render endpoint by walking the dashboard's declared aliases through
/// their <see cref="EntityAliasResolver"/> implementations and freezing the
/// outcomes into <see cref="WidgetRenderContext.ResolvedEntityAliases"/>.
/// Renderers read bindings from the context — they never call resolvers
/// directly, keeping the renderer side stateless and easy to test.
/// </summary>
/// <param name="AliasName">The alias key as declared on the dashboard (e.g. <c>"currentDevice"</c>, <c>"viewedCustomer"</c>).</param>
/// <param name="EntityType">The resolver-supplied entity type discriminator (e.g. <c>"Device"</c>, <c>"Customer"</c>) — lets renderers verify they got the kind they expected before consuming the id.</param>
/// <param name="EntityId">The concrete id the alias resolves to. <see langword="null"/> when the alias could not be resolved (route param missing, current-tenant indeterminate, …) — renderers consuming an unresolved alias return <see cref="WidgetSnapshotStatus.Unavailable"/>.</param>
public sealed record EntityAliasBinding(
    string AliasName,
    string EntityType,
    string? EntityId);
