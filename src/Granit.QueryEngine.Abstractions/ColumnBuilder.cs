using Granit.DataLookup.Descriptors;

namespace Granit.QueryEngine;

/// <summary>
/// Fluent builder for configuring a single column in a <see cref="QueryDefinitionBuilder{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
public sealed class ColumnBuilder<TEntity> where TEntity : class
{
    internal string? LabelValue { get; private set; }
    internal string? LabelKeyValue { get; private set; }
    internal int OrderValue { get; private set; }
    internal bool IsSortableValue { get; private set; }
    internal bool IsFilterableValue { get; private set; }
    internal bool IsVisibleValue { get; private set; } = true;
    internal string? FormatValue { get; private set; }
    internal LookupDescriptor? LookupValue { get; private set; }
    internal string? CurrencyCodeValue { get; private set; }

    /// <summary>
    /// Sets the user-facing label for this column.
    /// </summary>
    /// <param name="label">The display label.</param>
    public ColumnBuilder<TEntity> Label(string label)
    {
        LabelValue = label;
        return this;
    }

    /// <summary>
    /// Sets a localization key for the column label. When a localizer is available,
    /// this key is resolved to a culture-specific string. Falls back to <see cref="Label"/>
    /// or the property name if the key is not found.
    /// </summary>
    /// <param name="key">The localization key (e.g. <c>"Scheduling.Columns.PayloadType"</c>).</param>
    public ColumnBuilder<TEntity> LabelKey(string key)
    {
        LabelKeyValue = key;
        return this;
    }

    /// <summary>
    /// Sets the display order for this column (lower values first).
    /// </summary>
    /// <param name="order">The sort order index.</param>
    public ColumnBuilder<TEntity> Order(int order)
    {
        OrderValue = order;
        return this;
    }

    /// <summary>
    /// Marks this column as sortable. Columns are not sortable by default (whitelist-first).
    /// </summary>
    /// <param name="sortable">Whether sorting is enabled.</param>
    public ColumnBuilder<TEntity> Sortable(bool sortable = true)
    {
        IsSortableValue = sortable;
        return this;
    }

    /// <summary>
    /// Marks this column as filterable. Columns are not filterable by default (whitelist-first).
    /// </summary>
    /// <param name="filterable">Whether filtering is enabled.</param>
    public ColumnBuilder<TEntity> Filterable(bool filterable = true)
    {
        IsFilterableValue = filterable;
        return this;
    }

    /// <summary>
    /// Sets whether this column is visible by default. Default is <c>true</c>.
    /// </summary>
    /// <param name="visible">Whether the column is visible.</param>
    public ColumnBuilder<TEntity> Visible(bool visible = true)
    {
        IsVisibleValue = visible;
        return this;
    }

    /// <summary>
    /// Sets a format hint for the column (e.g. <c>"dd/MM/yyyy"</c> for dates).
    /// </summary>
    /// <param name="format">The format string.</param>
    public ColumnBuilder<TEntity> Format(string format)
    {
        FormatValue = format;
        return this;
    }

    /// <summary>
    /// Declares a data-lookup source for this column by registry name. The frontend
    /// renders a typeahead picker backed by
    /// <c>GET /lookups/{name}</c> instead of a free-text filter input.
    /// </summary>
    /// <remarks>
    /// Preferred form when the backing source is registered in the central
    /// <c>ILookupRegistry</c>. For one-off lookups pointing to an ad-hoc HTTP
    /// endpoint, use the overload accepting a full <see cref="LookupDescriptor"/>.
    /// </remarks>
    /// <param name="name">Registry key of the lookup (e.g. <c>"tenants"</c>).</param>
    /// <param name="kind">Kind of backing source — informational, helps the UI pick an affordance.</param>
    /// <param name="requiredPermission">Permission the caller must hold to open the picker.</param>
    /// <param name="scopeKeys">Scope keys the source requires (e.g. <c>["tenantId"]</c>).</param>
    public ColumnBuilder<TEntity> Lookup(
        string name,
        LookupKind kind = LookupKind.QueryEngine,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        LookupValue = new LookupDescriptor(
            Name: name,
            Kind: kind,
            RequiredPermission: requiredPermission,
            ScopeKeys: scopeKeys);
        return this;
    }

    /// <summary>
    /// Declares a data-lookup source for this column via a full
    /// <see cref="LookupDescriptor"/>. Use when pointing to a custom URL
    /// (<see cref="LookupDescriptor.Endpoint"/>) or when overriding the default
    /// <see cref="LookupDescriptor.SearchParam"/>.
    /// </summary>
    /// <param name="descriptor">The descriptor to attach to this column.</param>
    public ColumnBuilder<TEntity> Lookup(LookupDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        LookupValue = descriptor;
        return this;
    }

    /// <summary>
    /// Tags this column as a monetary amount in the supplied ISO 4217 currency
    /// (e.g. <c>"EUR"</c>, <c>"USD"</c>, <c>"JPY"</c>). Surfaced on the wire so
    /// dashboard renderers (KPI / Chart / Table / Pivot) emit the
    /// <c>"Currency"</c> value-kind with the right currency code, letting the
    /// frontend pick the matching locale + symbol when formatting.
    /// </summary>
    /// <remarks>
    /// Per-column metadata (not per-entity) — different columns on the same
    /// entity can carry different currencies (e.g. <c>AmountEur</c> /
    /// <c>AmountUsd</c>). The framework treats the code as opaque text;
    /// downstream rendering may validate against the ISO 4217 list. Use the
    /// uppercase three-letter code by convention.
    /// </remarks>
    /// <param name="isoCode">ISO 4217 alpha-3 currency code.</param>
    public ColumnBuilder<TEntity> Currency(string isoCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isoCode);
        CurrencyCodeValue = isoCode;
        return this;
    }
}
