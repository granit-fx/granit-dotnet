using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Forms;

/// <summary>
/// Fluent builder for an owned-collection section — combines section-level metadata
/// (label, collapsing) with the per-item form schema (item fields, item display
/// property, render cap).
/// </summary>
/// <typeparam name="TOwner">The owning entity type (the section's parent form's TEntity).</typeparam>
/// <typeparam name="TItem">The item CLR type held by the owner's collection navigation.</typeparam>
public sealed class OwnedCollectionSectionBuilder<TOwner, TItem>
{
    private readonly string _key;
    private readonly string _propertyName;
    private readonly int _order;
    private readonly List<Func<FieldDescriptor>> _itemFieldFactories = [];

    private string? _labelKey;
    private bool _collapsedByDefault;
    private string? _itemDisplayProperty;
    private int? _maxRendered;
    private int _nextItemFieldOrder;

    internal OwnedCollectionSectionBuilder(string key, string propertyName, int order)
    {
        _key = key;
        _propertyName = propertyName;
        _order = order;
    }

    /// <summary>i18n key for the section header (resolved client-side).</summary>
    public OwnedCollectionSectionBuilder<TOwner, TItem> Label(string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        _labelKey = labelKey;
        return this;
    }

    /// <summary>Marks the section as collapsed by default in the renderer.</summary>
    public OwnedCollectionSectionBuilder<TOwner, TItem> CollapsedByDefault()
    {
        _collapsedByDefault = true;
        return this;
    }

    /// <summary>
    /// Adds a per-item field. The lambda must be a direct property access on
    /// <typeparamref name="TItem"/> (e.g. <c>a =&gt; a.Line1</c>). Configuration follows
    /// the same fluent shape as a scalar <see cref="SectionBuilder{TEntity}.Field"/>.
    /// </summary>
    public OwnedCollectionSectionBuilder<TOwner, TItem> ItemField<TProperty>(
        Expression<Func<TItem, TProperty>> propertySelector,
        Action<FieldBuilder<TItem, TProperty>>? configure = null)
    {
        int order = _nextItemFieldOrder++;
        FieldBuilder<TItem, TProperty> builder = new(propertySelector, order);
        configure?.Invoke(builder);
        _itemFieldFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Names the item property used as the collapsed-row headline (e.g. <c>"Line1"</c>
    /// for an address). The lambda must be a direct property access on
    /// <typeparamref name="TItem"/>; the field must also have been declared via
    /// <see cref="ItemField"/> for the value to appear in the payload.
    /// </summary>
    public OwnedCollectionSectionBuilder<TOwner, TItem> ItemDisplayProperty<TProperty>(
        Expression<Func<TItem, TProperty>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "ItemDisplayProperty selector must be a direct property access expression (e.g. a => a.Line1).",
                nameof(propertySelector));
        }

        _itemDisplayProperty = property.Name;
        return this;
    }

    /// <summary>Caps how many items the renderer expands inline before paging (renderer hint).</summary>
    public OwnedCollectionSectionBuilder<TOwner, TItem> MaxRendered(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be a positive integer.");
        }
        _maxRendered = max;
        return this;
    }

    internal SectionDescriptor Build() =>
        new()
        {
            Key = _key,
            LabelKey = _labelKey,
            Order = _order,
            Fields = [],
            CollapsedByDefault = _collapsedByDefault,
            OwnedCollection = new OwnedCollectionDescriptor
            {
                PropertyName = _propertyName,
                ItemType = typeof(TItem),
                ItemFields = [.. _itemFieldFactories.Select(f => f())],
                ItemDisplayProperty = _itemDisplayProperty,
                MaxRendered = _maxRendered,
            },
        };
}
