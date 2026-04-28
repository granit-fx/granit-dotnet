namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Response payload for a single entry in <c>GET /dashboards/catalog</c>. Mirrors
/// the relevant subset of <see cref="IDashboardDefinitionDescriptor"/> on the wire,
/// stripping framework internals (the registry, type-erased descriptors) so the
/// frontend has a predictable shape to consume.
/// </summary>
/// <param name="Name">Wire identifier — e.g. <c>"Granit.Invoicing.FinanceOverview"</c>.</param>
/// <param name="Category">Coarse grouping — see <see cref="DashboardCategory"/>.</param>
/// <param name="IsSystem">When <c>true</c>, imported instances cannot be deleted by tenant admins (only re-synced).</param>
/// <param name="Version">Semver of the definition shape — used by the drift-detection UI.</param>
/// <param name="WidgetCount">Number of widgets shipped by this dashboard (single-view) or by its default view (multi-view).</param>
/// <param name="HasViews">Whether the dashboard ships multiple named views (P2.1).</param>
/// <param name="HasAliases">Whether the dashboard takes entity parameters (P2.3).</param>
/// <param name="HasFilters">Whether the dashboard ships toolbar / silent filters (P2.5).</param>
public sealed record DashboardCatalogEntryResponse(
    string Name,
    DashboardCategory Category,
    bool IsSystem,
    string Version,
    int WidgetCount,
    bool HasViews,
    bool HasAliases,
    bool HasFilters);
