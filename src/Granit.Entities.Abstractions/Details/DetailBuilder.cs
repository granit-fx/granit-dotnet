namespace Granit.Entities.Details;

/// <summary>
/// Fluent builder for one detail-view variant. Sections are added in declaration
/// order; the side panels are accessed via the <see cref="SidePanel"/> helper.
/// </summary>
/// <typeparam name="TEntity">The owning entity type.</typeparam>
public sealed class DetailBuilder<TEntity>
{
    private readonly string _name;
    private readonly List<Func<DetailSectionDescriptor>> _sectionFactories = [];
    private readonly SidePanelBuilder _sidePanel = new();
    private int _nextSectionOrder;

    internal DetailBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>Adds a section to the detail view.</summary>
    public DetailBuilder<TEntity> Section(string key, Action<DetailSectionBuilder<TEntity>>? configure = null)
    {
        int order = _nextSectionOrder++;
        DetailSectionBuilder<TEntity> builder = new(key, order);
        configure?.Invoke(builder);
        _sectionFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Convenience: clone every section of the named form variant into the detail view,
    /// each section configured to inherit from the form (read-mode rendering by default).
    /// Equivalent to one <see cref="Section"/> call per form section with
    /// <see cref="DetailSectionBuilder{TEntity}.InheritsFromForm"/>.
    /// </summary>
    public DetailBuilder<TEntity> SectionsFromForm(string variantName = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variantName);

        // The form variant must exist at the EntityDefinition level; we record the
        // intent here as a single virtual section that the manifest aggregator will
        // expand at request time once it has visibility into the form variants.
        return Section(variantName, s => s.InheritsFromForm(variantName));
    }

    /// <summary>The side-panel rail builder (chain via <c>.Audit().Timeline().Comments()</c>).</summary>
    public SidePanelBuilder SidePanel => _sidePanel;

    internal DetailDescriptor Build() =>
        new()
        {
            Name = _name,
            Sections = [.. _sectionFactories.Select(f => f())],
            SidePanels = _sidePanel.Build(),
        };
}
