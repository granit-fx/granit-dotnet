namespace Granit.Dashboards;

/// <summary>
/// Declarative dashboard catalogue entry shipped by a Granit module. Mirrors the
/// existing declarative primitives — pure declaration, no runtime, no persistence.
/// Pairs with <c>QueryDefinition</c> (lists rows), <c>ExportDefinition</c> (extracts
/// rows), and <c>MetricDefinition</c> (aggregates rows) — see ADR-020 and ADR-038.
/// </summary>
/// <remarks>
/// <para>
/// Each definition is registered as a singleton via
/// <c>services.AddDashboardDefinition&lt;TDefinition&gt;()</c>. Definitions describe
/// the dashboard catalogue surfaced to admins; they are not the persisted dashboard.
/// When an admin imports a definition (story B4), the framework deep-copies the
/// definition into a new <c>Dashboard</c> aggregate (story B2). Module upgrades do
/// NOT retro-edit imported dashboards — drift is surfaced via
/// <see cref="Version"/> + an admin-driven re-sync action (ADR-038 §3).
/// </para>
/// <para>
/// Example (analytics dashboard combining a KPI from <c>Granit.Analytics</c> and a
/// presentation-only widget from this package):
/// <code>
/// public sealed class InvoicingFinanceDashboard : DashboardDefinition
/// {
///     public override string Name =&gt; "Granit.Invoicing.FinanceOverview";
///     public override DashboardCategory Category =&gt; DashboardCategory.Finance;
///     public override IReadOnlyList&lt;WidgetDefinition&gt; Widgets { get; } = [
///         new MarkdownWidgetDefinition("Banner", "Widget:Granit.Invoicing.FinanceOverview.Banner", Position: 0),
///         new KpiWidgetDefinition("UnpaidCount", "Granit.Invoicing.UnpaidInvoiceCountMetric", Position: 1),
///     ];
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class DashboardDefinition : IDashboardDefinitionDescriptor
{
    /// <summary>
    /// Unique wire identifier — <c>"Granit.{Module}.{DashboardName}"</c>, PascalCase,
    /// dot-separated. Used as the resource key for the dashboard's localized title
    /// (<c>Dashboard:{Name}</c>) and as the import endpoint parameter
    /// (<c>POST /dashboards/from-definition/{name}</c>).
    /// </summary>
    public abstract string Name { get; }

    /// <summary>Catalogue grouping. Drives the section ordering in the import dialog.</summary>
    public abstract DashboardCategory Category { get; }

    /// <summary>
    /// Whether this dashboard is mandated by the platform operator. When <c>true</c>,
    /// imported instances cannot be deleted by tenant admins (only re-synced). Defaults
    /// to <c>false</c> — framework modules ship suggestions, not impositions
    /// (ADR-038 §5).
    /// </summary>
    public virtual bool IsSystem => false;

    /// <summary>
    /// Semver of the definition shape. Bumped when the widget set changes in a
    /// breaking way; surfaced to admins as drift after import (ADR-038 §3).
    /// </summary>
    public virtual string Version => "1.0.0";

    /// <summary>Grid layout configuration. Defaults to <see cref="DashboardLayout.Default"/>.</summary>
    public virtual DashboardLayout Layout => DashboardLayout.Default;

    /// <summary>
    /// Default time window applied to every data-bound widget that does not carry
    /// its own <see cref="WidgetDefinition.TimeWindowOverride"/>. <c>null</c> means
    /// "let the frontend pick its global default" — typical for a dashboard whose
    /// widgets each scope their own range explicitly.
    /// </summary>
    public virtual DashboardTimeWindow? DefaultTimeWindow => null;

    /// <summary>Widgets shipped by this dashboard, in declared order.</summary>
    public abstract IReadOnlyList<WidgetDefinition> Widgets { get; }

    /// <summary>
    /// Dashboard-scoped filters. Each filter may be referenced by name from a widget's
    /// data source; toolbar-exposed filters (<see cref="DashboardFilter.Editable"/>
    /// = <c>true</c>) become user-editable controls above the grid. <c>null</c> = the
    /// dashboard has no filters beyond what individual widgets carry.
    /// </summary>
    public virtual IReadOnlyList<DashboardFilter>? Filters => null;
}
