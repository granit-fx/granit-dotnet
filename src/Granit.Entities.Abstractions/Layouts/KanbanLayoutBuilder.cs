using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Layouts;

/// <summary>
/// Fluent builder for a kanban layout declaration. Mirrors Frappe's three
/// kanban settings: column field (group-by), title field (tile headline),
/// and body fields — plus per-column metadata (colour, default state) and
/// the framework-wide <see cref="EntityListLayoutDescriptor.IsDefault"/> /
/// <see cref="EntityListLayoutDescriptor.RequiresPermission"/> opt-ins.
/// </summary>
/// <typeparam name="TEntity">The entity rendered on the board.</typeparam>
/// <typeparam name="TGroupBy">CLR type of the group-by property — typed so per-column values can't collide.</typeparam>
public sealed class KanbanLayoutBuilder<TEntity, TGroupBy>
    where TGroupBy : notnull
{
    private readonly List<Func<KanbanColumnDescriptor>> _columnFactories = [];

    private string? _groupByPropertyName;
    private KanbanCardBuilder<TEntity>? _cardBuilder;
    private bool _isDefault;
    private string? _requiresPermission;

    internal KanbanLayoutBuilder() { }

    /// <summary>
    /// Names the property used to bucket rows into columns. The lambda must be
    /// a direct property access (e.g. <c>i =&gt; i.Status</c>); only enums and
    /// lookup-table values (finite discrete sets) are supported in v1.
    /// </summary>
    public KanbanLayoutBuilder<TEntity, TGroupBy> GroupBy(Expression<Func<TEntity, TGroupBy>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "GroupBy selector must be a direct property access expression (e.g. i => i.Status).",
                nameof(propertySelector));
        }

        _groupByPropertyName = property.Name;
        return this;
    }

    /// <summary>Configures the card-content schema rendered inside each tile.</summary>
    public KanbanLayoutBuilder<TEntity, TGroupBy> Card(Action<KanbanCardBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        KanbanCardBuilder<TEntity> builder = new();
        configure(builder);
        _cardBuilder = builder;
        return this;
    }

    /// <summary>
    /// Declares one column with optional colour and default state. Values are
    /// strongly typed by <typeparamref name="TGroupBy"/>; serialised via
    /// <c>ToString()</c> (enum members keep their member name).
    /// </summary>
    public KanbanLayoutBuilder<TEntity, TGroupBy> Column(
        TGroupBy value,
        Action<KanbanColumnBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        KanbanColumnBuilder builder = new();
        configure?.Invoke(builder);
        string serialised = value.ToString() ?? throw new InvalidOperationException(
            $"Kanban column value of type '{typeof(TGroupBy).FullName}' returned a null ToString().");
        _columnFactories.Add(() => builder.Build(serialised));
        return this;
    }

    /// <summary>
    /// Marks this layout as the one the renderer picks on first load. At most
    /// one layout per entity may opt in.
    /// </summary>
    public KanbanLayoutBuilder<TEntity, TGroupBy> IsDefault()
    {
        _isDefault = true;
        return this;
    }

    /// <summary>
    /// Drops the layout from the manifest payload entirely when the user lacks
    /// this permission — defense in depth, never just hidden.
    /// </summary>
    public KanbanLayoutBuilder<TEntity, TGroupBy> RequiresPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _requiresPermission = permissionName;
        return this;
    }

    internal KanbanLayoutDescriptor Build()
    {
        if (_groupByPropertyName is null)
        {
            throw new InvalidOperationException(
                "KanbanView requires GroupBy(...) — declare which property buckets rows into columns.");
        }

        KanbanCardDescriptor card = _cardBuilder?.Build() ?? new KanbanCardDescriptor { Fields = [] };

        return new KanbanLayoutDescriptor
        {
            Kind = EntityListLayoutKind.Kanban,
            IsDefault = _isDefault,
            RequiresPermission = _requiresPermission,
            GroupByPropertyName = _groupByPropertyName,
            GroupByClrType = typeof(TGroupBy),
            Card = card,
            Columns = [.. _columnFactories.Select(f => f())],
        };
    }
}
