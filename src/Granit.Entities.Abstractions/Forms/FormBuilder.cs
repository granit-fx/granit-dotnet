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
    /// Adds a section to the form. The <paramref name="key"/> is the stable address used
    /// for i18n keys and tenant layout customization (Phase 2).
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
}
