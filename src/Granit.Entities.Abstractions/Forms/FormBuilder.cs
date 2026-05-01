using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Forms;

/// <summary>
/// Fluent builder for one form variant. Sections are added in declaration order.
/// </summary>
/// <typeparam name="TEntity">The owning entity type.</typeparam>
public sealed class FormBuilder<TEntity>
{
    private readonly string _name;
    private readonly List<Func<SectionDescriptor>> _sectionFactories = [];

    private bool _customizable;
    private int _nextSectionOrder;

    internal FormBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>
    /// Adds a scalar section to the form. The <paramref name="key"/> is the stable address
    /// used for i18n keys and tenant layout customization (Phase 2).
    /// </summary>
    public FormBuilder<TEntity> Section(string key, Action<SectionBuilder<TEntity>>? configure = null)
    {
        int order = _nextSectionOrder++;
        SectionBuilder<TEntity> builder = new(key, order);
        configure?.Invoke(builder);
        _sectionFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Adds an owned-collection section to the form. The <paramref name="collectionSelector"/>
    /// must be a direct property access pointing at an <see cref="IEnumerable{T}"/>-shaped
    /// navigation on the owner (e.g. <c>p =&gt; p.Addresses</c>); the per-item form schema
    /// is configured via <paramref name="configure"/>.
    /// </summary>
    public FormBuilder<TEntity> OwnedCollectionSection<TItem>(
        string key,
        Expression<Func<TEntity, IEnumerable<TItem>>> collectionSelector,
        Action<OwnedCollectionSectionBuilder<TEntity, TItem>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(configure);

        string propertyName = ParseCollectionSelector(collectionSelector);

        int order = _nextSectionOrder++;
        OwnedCollectionSectionBuilder<TEntity, TItem> builder = new(key, propertyName, order);
        configure(builder);
        _sectionFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Opt the form into tenant-admin layer-1 customization (per ADR-040 Tier B Layer 1).
    /// Tenant admins may reorder / regroup / hide compiled fields; they can NEVER add new
    /// fields, change validation, or change widget.
    /// </summary>
    public FormBuilder<TEntity> Customizable()
    {
        _customizable = true;
        return this;
    }

    internal FormDescriptor Build() =>
        new()
        {
            Name = _name,
            Sections = [.. _sectionFactories.Select(f => f())],
            Customizable = _customizable,
        };

    private static string ParseCollectionSelector<TItem>(
        Expression<Func<TEntity, IEnumerable<TItem>>> selector)
    {
        if (selector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Collection selector must be a direct property access expression (e.g. p => p.Addresses).",
                nameof(selector));
        }

        return property.Name;
    }
}
