using System.Linq.Expressions;

namespace Granit.Entities.Forms;

/// <summary>
/// Fluent builder for one form section: header label + ordered list of fields.
/// </summary>
/// <typeparam name="TEntity">The owning entity type.</typeparam>
public sealed class SectionBuilder<TEntity>
{
    private readonly string _key;
    private readonly List<Func<FieldDescriptor>> _fieldFactories = [];
    private readonly int _order;

    private string? _labelKey;
    private bool _collapsedByDefault;
    private int _nextFieldOrder;

    internal SectionBuilder(string key, int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
        _order = order;
    }

    /// <summary>i18n key for the section header (resolved client-side).</summary>
    public SectionBuilder<TEntity> Label(string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        _labelKey = labelKey;
        return this;
    }

    /// <summary>Marks the section as collapsed by default in the renderer.</summary>
    public SectionBuilder<TEntity> CollapsedByDefault()
    {
        _collapsedByDefault = true;
        return this;
    }

    /// <summary>
    /// Adds a field to the section. The lambda must be a direct property access
    /// (<c>x =&gt; x.Title</c>); the resulting descriptor stores the property name + CLR
    /// type for the renderer to consume.
    /// </summary>
    public SectionBuilder<TEntity> Field<TProperty>(
        Expression<Func<TEntity, TProperty>> propertySelector,
        Action<FieldBuilder<TEntity, TProperty>>? configure = null)
    {
        int order = _nextFieldOrder++;
        FieldBuilder<TEntity, TProperty> builder = new(propertySelector, order);
        configure?.Invoke(builder);
        _fieldFactories.Add(builder.Build);
        return this;
    }

    internal SectionDescriptor Build() =>
        new()
        {
            Key = _key,
            LabelKey = _labelKey,
            Order = _order,
            Fields = [.. _fieldFactories.Select(f => f())],
            CollapsedByDefault = _collapsedByDefault,
        };
}
