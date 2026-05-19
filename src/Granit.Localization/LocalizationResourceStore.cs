// ---------------------------------------------------------------------------
// LocalizationResourceStore.cs
// Typed collection for registering and retrieving localization resources.
// Used in GranitLocalizationOptions.
// ---------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Granit.Localization;

/// <summary>
/// Dictionary of registered localization resources, indexed by marker type.
/// </summary>
public sealed class LocalizationResourceStore
{
    private readonly Dictionary<Type, LocalizationResourceInfo> _resources = [];

    /// <summary>
    /// Registers a new localization resource.
    /// If the type is already registered, the entry is replaced.
    /// </summary>
    /// <typeparam name="TResource">Marker type of the resource.</typeparam>
    /// <param name="defaultCulture">Default culture (default: "fr").</param>
    /// <returns>Resource info for fluent chaining.</returns>
    public LocalizationResourceInfo Add<TResource>(string defaultCulture = "fr")
        => Add(typeof(TResource), defaultCulture);

    /// <summary>
    /// Registers a new localization resource (non-generic overload).
    /// Used by auto-discovery to dynamically register discovered types.
    /// If the type is already registered, the entry is replaced.
    /// </summary>
    /// <param name="resourceType">Marker type of the resource.</param>
    /// <param name="defaultCulture">Default culture (default: "fr").</param>
    /// <returns>Resource info for fluent chaining.</returns>
    public LocalizationResourceInfo Add(Type resourceType, string defaultCulture = "fr")
    {
        LocalizationResourceInfo info = new(resourceType, defaultCulture);
        _resources[resourceType] = info;
        return info;
    }

    /// <summary>
    /// Retrieves the registered resource for the given type.
    /// </summary>
    /// <typeparam name="TResource">Marker type of the resource.</typeparam>
    /// <returns>Resource info.</returns>
    /// <exception cref="KeyNotFoundException">If the type is not registered.</exception>
    public LocalizationResourceInfo Get<TResource>() =>
        _resources[typeof(TResource)];

    /// <summary>
    /// Attempts to retrieve the resource for the given type.
    /// </summary>
    /// <param name="resourceType">Marker type of the resource.</param>
    /// <param name="info">Resource info if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool TryGetValue(Type resourceType, [NotNullWhen(true)] out LocalizationResourceInfo? info) =>
        _resources.TryGetValue(resourceType, out info);

    /// <summary>
    /// Returns all registered resources.
    /// </summary>
    public IEnumerable<LocalizationResourceInfo> GetAll() =>
        _resources.Values;
}
