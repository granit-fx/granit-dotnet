namespace Granit.Dashboards;

/// <summary>
/// Dashboard-scoped filter applied to data-bound widgets that opt in. P2.5 of the
/// dashboards-architecture-proposals roadmap — distinct from per-datasource filters
/// a widget may carry internally:
/// </summary>
/// <list type="bullet">
///   <item><b>Per-widget filter</b> (today): "this chart only ever shows unpaid invoices".
///         Lives inside the widget's data source.</item>
///   <item><b>Dashboard filter</b> (here): "this whole dashboard is now scoped to
///         March 2026" / "Customer = X". Shared across many widgets, optionally
///         user-editable from a toolbar control above the grid.</item>
/// </list>
/// <remarks>
/// <para>
/// Toolbar-exposed filters (<see cref="Editable"/> = <c>true</c>) become user-driven
/// controls; the frontend renders the appropriate input widget (date picker, lookup,
/// text, ...) based on the clause shape, and propagates value changes through
/// <c>DashboardContext</c> to refetch every widget that references this filter by
/// <see cref="Name"/>.
/// </para>
/// <para>
/// Non-editable filters apply silently — useful for "current user", "current tenant"
/// scoping that should never become a user-toggleable control.
/// </para>
/// </remarks>
/// <param name="Name">
/// Filter identifier referenced from a data source's <c>DashboardFilters</c> list.
/// PascalCase, unique within the dashboard (e.g. <c>"CurrentCustomer"</c>,
/// <c>"DateRange"</c>, <c>"Severity"</c>).
/// </param>
/// <param name="LabelLocalizationKey">
/// Localization key for the toolbar label when <see cref="Editable"/> is <c>true</c>.
/// Resolved by the standard Granit localization stack.
/// </param>
/// <param name="Clauses">
/// One or more clauses combined by <see cref="Operation"/>. Static values are encoded
/// as their string representation; <c>${variable}</c> references resolve via
/// <c>IVariableSubstituter</c> at query-build time against state params and aliases.
/// </param>
/// <param name="Operation">
/// How clauses combine — <see cref="DashboardFilterOperation.And"/> (every clause must
/// match) or <see cref="DashboardFilterOperation.Or"/>. Defaults to <c>And</c>.
/// </param>
/// <param name="Editable">
/// When <c>true</c>, the filter is rendered as a toolbar control above the grid; end
/// users can change its value at runtime. Defaults to <c>false</c> — silent application.
/// </param>
public sealed record DashboardFilter(
    string Name,
    string LabelLocalizationKey,
    IReadOnlyList<DashboardFilterClause> Clauses,
    DashboardFilterOperation Operation = DashboardFilterOperation.And,
    bool Editable = false);

/// <summary>
/// Single clause inside a <see cref="DashboardFilter"/>.
/// </summary>
/// <param name="Field">Field path (e.g. <c>"customer.id"</c>, <c>"issuedAt"</c>, <c>"status"</c>).</param>
/// <param name="Op">Comparison operator — see <see cref="DashboardFilterOperator"/>.</param>
/// <param name="Value">
/// Static value (string-encoded for JSON parity with the QueryEngine wire format) or a
/// <c>${variable}</c> placeholder resolved at query-build time. <c>null</c> is valid
/// for operators that test for absence (combined with negation upstream).
/// </param>
public sealed record DashboardFilterClause(
    string Field,
    DashboardFilterOperator Op,
    string? Value);

/// <summary>Comparison operator used by <see cref="DashboardFilterClause"/>.</summary>
/// <remarks>
/// Names match the OData / Granit.QueryEngine wire vocabulary (<c>eq</c>, <c>ne</c>,
/// <c>gt</c>, <c>gte</c>, <c>lt</c>, <c>lte</c>, <c>in</c>, <c>contains</c>,
/// <c>startswith</c>) so frontend code consuming dashboard filters can reuse the same
/// operator dictionary already used for grid query strings.
/// </remarks>
public enum DashboardFilterOperator
{
    /// <summary>Equal.</summary>
    Eq = 0,

    /// <summary>Not equal.</summary>
    Ne = 1,

    /// <summary>Greater than.</summary>
    Gt = 2,

    /// <summary>Greater than or equal.</summary>
    Gte = 3,

    /// <summary>Less than.</summary>
    Lt = 4,

    /// <summary>Less than or equal.</summary>
    Lte = 5,

    /// <summary>Value belongs to a comma-separated set (encoded inside <see cref="DashboardFilterClause.Value"/>).</summary>
    In = 6,

    /// <summary>String contains the value (case-sensitive).</summary>
    Contains = 7,

    /// <summary>String starts with the value (case-sensitive).</summary>
    StartsWith = 8,
}

/// <summary>How a <see cref="DashboardFilter"/>'s clauses combine.</summary>
public enum DashboardFilterOperation
{
    /// <summary>Every clause must match. The default — narrowest scope.</summary>
    And = 0,

    /// <summary>At least one clause must match.</summary>
    Or = 1,
}
