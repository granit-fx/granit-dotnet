using System.Text.Json.Serialization;

namespace Granit.Dashboards;

/// <summary>
/// How an <see cref="EntityAlias"/> resolves to a concrete entity at render time.
/// Five base kinds shipped today; <c>RelationGraphResolver</c> (TB-style traversal)
/// is intentionally deferred to its own ADR — it requires a relation registry that
/// the framework does not have yet, and the four resolvers below cover every
/// applicative use case present in the current backlog.
/// </summary>
/// <remarks>
/// JSON polymorphism uses the <c>"kind"</c> discriminator with short kebab tags
/// (<c>route-param</c>, <c>view-entity</c>, <c>tenant-context</c>,
/// <c>user-selection</c>, <c>static</c>). Stable wire format — same approach as
/// <see cref="WidgetDefinition"/>'s <c>"type"</c> discriminator.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RouteParamResolver), "route-param")]
[JsonDerivedType(typeof(ViewEntityResolver), "view-entity")]
[JsonDerivedType(typeof(TenantContextResolver), "tenant-context")]
[JsonDerivedType(typeof(UserSelectionResolver), "user-selection")]
[JsonDerivedType(typeof(StaticEntityResolver), "static")]
public abstract record EntityAliasResolver;

/// <summary>
/// Pulls the entity id from a URL / route parameter — typical for "open this
/// dashboard for entity X" patterns where the parent route owns the binding.
/// </summary>
/// <param name="ParamName">Route parameter name (e.g. <c>"id"</c>, <c>"customerId"</c>).</param>
public sealed record RouteParamResolver(string ParamName) : EntityAliasResolver;

/// <summary>
/// Pulls the entity id from the active <see cref="DashboardView"/>'s parameters
/// — the bridge between the view stack (P2.1) and aliases. When a row click in a
/// list view pushes a new view onto the stack with <c>entityId</c> in its params,
/// the alias re-resolves automatically and downstream widgets re-fetch with the
/// new entity. Renamed from the proposal's <c>StateEntityResolver</c> per the
/// agreed States → Views rename.
/// </summary>
/// <param name="ParamName">Slot name in the view's params dictionary. Defaults to <c>"entityId"</c>.</param>
public sealed record ViewEntityResolver(string ParamName = "entityId") : EntityAliasResolver;

/// <summary>
/// Resolves to the current authenticated tenant — single entity. Carries no
/// payload; the runtime reads <c>ICurrentTenant</c> at render time.
/// </summary>
public sealed record TenantContextResolver : EntityAliasResolver;

/// <summary>
/// Renders a picker; the user selects one (or many) entities at runtime. Backs
/// "scope this dashboard to a customer of my choosing" patterns without forcing
/// the caller to encode the entity id in the URL.
/// </summary>
/// <param name="LookupName">Granit.DataLookup name backing the picker.</param>
/// <param name="MultiSelect">When <c>true</c>, the picker accepts a multi-selection set.</param>
public sealed record UserSelectionResolver(
    string LookupName,
    bool MultiSelect = false) : EntityAliasResolver;

/// <summary>
/// Hard-coded entity reference. Mostly for testing or "global" widgets pinned to
/// a known operator-level entity.
/// </summary>
/// <param name="EntityId">Stringified entity identifier.</param>
public sealed record StaticEntityResolver(string EntityId) : EntityAliasResolver;
