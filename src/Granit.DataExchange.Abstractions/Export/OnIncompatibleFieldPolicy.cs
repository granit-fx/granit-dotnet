namespace Granit.DataExchange.Export;

/// <summary>
/// Controls how the export pipeline behaves when the chosen format writer does not support
/// the complex/hierarchical fields declared in an <see cref="ExportDefinition{TEntity}"/>.
/// </summary>
public enum OnIncompatibleFieldPolicy
{
    /// <summary>
    /// Throw <see cref="Exceptions.ExportProviderIncompatibleException"/> immediately.
    /// This is the default: fail fast rather than silently losing data.
    /// </summary>
    Throw,

    /// <summary>
    /// Drop the complex fields from the export and continue with the remaining scalar fields.
    /// A warning is logged for each skipped field.
    /// Use this when a thin tabular export is acceptable as a fallback.
    /// </summary>
    Skip,
}
