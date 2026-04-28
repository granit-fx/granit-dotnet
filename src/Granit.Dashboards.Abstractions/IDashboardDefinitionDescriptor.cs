namespace Granit.Dashboards;

/// <summary>
/// Type-erased descriptor exposed for registry lookup. Lets the registry surface every
/// registered <see cref="DashboardDefinition"/> without each consumer needing the
/// concrete type.
/// </summary>
/// <remarks>
/// User-facing strings are not on the descriptor — they are resolved from
/// localization keys composed off the descriptor's <see cref="Name"/>:
///
/// <list type="bullet">
///   <item><c>Dashboard:{Name}</c> — display title (required, ships in all 18 cultures).</item>
///   <item><c>Dashboard:{Name}.Description</c> — secondary description shown in the
///         catalogue / import dialog (optional). Modules ship the keys for their
///         default cultures; tenants override them via <c>Granit.Localization.Overrides</c>.</item>
///   <item><c>Widget:{Name}.{Slug}</c> — per-widget content (markdown body, image alt
///         text, plain text body, KPI title, ...). The exact key is on each widget's
///         <c>*LocalizationKey</c> property — this convention exists so the catalogue
///         can pre-fetch them in one batch.</item>
/// </list>
/// </remarks>
public interface IDashboardDefinitionDescriptor
{
    /// <summary>Unique wire identifier, e.g. <c>"Granit.Invoicing.FinanceOverview"</c>.</summary>
    string Name { get; }

    /// <summary>Coarse grouping driving catalogue ordering.</summary>
    DashboardCategory Category { get; }

    /// <summary>Whether the dashboard is mandated by the platform operator (cannot be deleted by tenant admins).</summary>
    bool IsSystem { get; }

    /// <summary>Semver — used for drift detection between definition and persisted dashboard.</summary>
    string Version { get; }

    /// <summary>Layout configuration applied to the widget grid.</summary>
    DashboardLayout Layout { get; }

    /// <summary>Widgets shipped by this dashboard, in declared order.</summary>
    IReadOnlyList<WidgetDefinition> Widgets { get; }
}
