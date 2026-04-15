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
}
