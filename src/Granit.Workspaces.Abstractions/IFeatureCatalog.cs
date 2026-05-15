namespace Granit.Workspaces;

/// <summary>
/// Registration-time builder passed to <see cref="IFeatureProvider"/> instances
/// to declare features (per ADR-057). Modules add features via
/// <see cref="Add(string, Action{FeatureBuilder})"/>; the host's composer
/// freezes the collected entries into an immutable
/// <see cref="IFeatureCatalog"/> after every provider has run.
/// </summary>
public interface IFeatureCatalogBuilder
{
    /// <summary>
    /// Adds a feature to the catalog under the supplied wire name.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown at boot if another provider already declared a feature with the
    /// same name — the framework requires globally unique feature names.
    /// </exception>
    void Add(string name, Action<FeatureBuilder> configure);
}

/// <summary>
/// Immutable read-only view of the feature catalog (per ADR-057). The host's
/// composer queries this after every <see cref="IFeatureProvider"/> has run,
/// and resolves <see cref="WorkspaceItemKind.Feature"/> items against it.
/// </summary>
public interface IFeatureCatalog
{
    /// <summary>
    /// All declared features, ordered by <see cref="FeatureDescriptor.Name"/>.
    /// </summary>
    IReadOnlyList<FeatureDescriptor> All { get; }

    /// <summary>
    /// Returns the feature with the supplied name, or <see langword="null"/>
    /// when no provider declared it. Workspace composition that references a
    /// missing feature fails fast at startup — runtime callers can rely on the
    /// catalog being complete.
    /// </summary>
    FeatureDescriptor? Find(string name);
}
