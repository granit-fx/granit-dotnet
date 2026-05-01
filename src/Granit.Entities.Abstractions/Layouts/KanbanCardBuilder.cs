using System.Linq.Expressions;
using System.Reflection;
using Granit.Entities.Forms;

namespace Granit.Entities.Layouts;

/// <summary>
/// Fluent sub-builder for the kanban-tile card schema. Reuses the form
/// <see cref="FieldBuilder{TEntity, TProperty}"/> for body fields so widget
/// overrides, permission gating and visibility rules apply consistently.
/// </summary>
/// <typeparam name="TEntity">The entity type rendered in the card.</typeparam>
public sealed class KanbanCardBuilder<TEntity>
{
    private readonly List<Func<FieldDescriptor>> _fieldFactories = [];

    private string? _titleProperty;
    private int _nextFieldOrder;

    internal KanbanCardBuilder() { }

    /// <summary>
    /// Names the property used as the tile headline. The lambda must be a
    /// direct property access (e.g. <c>i =&gt; i.InvoiceNumber</c>); when omitted
    /// the renderer falls back to the entity's <c>DisplayProperty</c>.
    /// </summary>
    public KanbanCardBuilder<TEntity> Title<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Title selector must be a direct property access expression (e.g. i => i.InvoiceNumber).",
                nameof(propertySelector));
        }

        _titleProperty = property.Name;
        return this;
    }

    /// <summary>
    /// Adds a body field rendered inside the card. Same lambda + configuration
    /// shape as form <see cref="SectionBuilder{TEntity}.Field"/>.
    /// </summary>
    public KanbanCardBuilder<TEntity> Field<TProperty>(
        Expression<Func<TEntity, TProperty>> propertySelector,
        Action<FieldBuilder<TEntity, TProperty>>? configure = null)
    {
        int order = _nextFieldOrder++;
        FieldBuilder<TEntity, TProperty> builder = new(propertySelector, order);
        configure?.Invoke(builder);
        _fieldFactories.Add(builder.Build);
        return this;
    }

    internal KanbanCardDescriptor Build() =>
        new()
        {
            TitleProperty = _titleProperty,
            Fields = [.. _fieldFactories.Select(f => f())],
        };
}
