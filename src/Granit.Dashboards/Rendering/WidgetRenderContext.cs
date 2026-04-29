using System.Security.Claims;
using Granit.Analytics;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// Per-render-request context passed to every <see cref="IWidgetInstanceRenderer"/>
/// when the dashboard render endpoint composes the widget pool. Built once at
/// the endpoint entry from the request payload, the current
/// <see cref="ClaimsPrincipal"/>, the resolved period, and the dashboard's
/// resolved entity aliases — then handed unchanged to every renderer so the
/// inputs to the cache-key recipe (see ADR-039 §5) stay uniform across kinds.
/// </summary>
/// <param name="TenantId">The current tenant id, or <see langword="null"/> for global / platform-admin renders. Always part of the cache key (<c>"global"</c> when null) — security boundary.</param>
/// <param name="User">The authenticated principal. Used by the dashboard renderer's permission gate before the typed renderer runs (see ADR-039 §3.a).</param>
/// <param name="Period">The resolved period window (UTC, half-open <c>[from, to)</c>) — already resolved against <c>IClock</c> at the endpoint entry. <see langword="null"/> when the dashboard does not declare a global period filter.</param>
/// <param name="Locale">The active locale (BCP-47 tag — e.g. <c>"en"</c>, <c>"fr-CA"</c>). Used by renderers that pre-format their snapshot text (e.g. localized date headers on chart buckets).</param>
/// <param name="DashboardFilters">Dashboard-level filter bindings, applied uniformly across widgets that share the same underlying entity (e.g. <c>{ "Status", "Open" }</c> drives the unpaid-invoices KPI and the unpaid-invoices table on the same dashboard).</param>
/// <param name="ResolvedEntityAliases">Frozen snapshot of every <see cref="EntityAlias"/> declared on the dashboard, resolved once at the endpoint entry — see <see cref="EntityAliasBinding"/>.</param>
public sealed record WidgetRenderContext(
    Guid? TenantId,
    ClaimsPrincipal User,
    ResolvedPeriod? Period,
    string Locale,
    IReadOnlyDictionary<string, string> DashboardFilters,
    IReadOnlyDictionary<string, EntityAliasBinding> ResolvedEntityAliases);
