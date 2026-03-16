// ---------------------------------------------------------------------------
// LocalizationResourceInfo.cs
// Represents a registered localization resource: marker type,
// default culture, embedded JSON sources, and inheritance chain.
// Fluent API for registration (AddJson, AddBaseTypes).
// ---------------------------------------------------------------------------

using System.Reflection;
using Granit.Localization.Internal;

namespace Granit.Localization;

/// <summary>
/// Registration information for a localization resource.
/// </summary>
/// <remarks>
/// Creates a new localization resource info.
/// </remarks>
/// <param name="resourceType">Marker type of the resource.</param>
/// <param name="defaultCulture">Default culture.</param>
public sealed class LocalizationResourceInfo(Type resourceType, string defaultCulture)
{
    /// <summary>
    /// Marker type of the resource (empty class with attributes).
    /// </summary>
    public Type ResourceType { get; } = resourceType;

    /// <summary>
    /// Default culture for this resource (e.g. "fr").
    /// </summary>
    public string DefaultCulture { get; } = defaultCulture;

    /// <summary>
    /// Types of parent resources (translation inheritance).
    /// </summary>
    public List<Type> BaseTypes { get; } = [];

    /// <summary>
    /// Embedded JSON sources associated with this resource.
    /// </summary>
    internal List<EmbeddedJsonSource> JsonSources { get; } = [];

    /// <summary>
    /// Adds a source of embedded JSON files.
    /// </summary>
    /// <param name="assembly">Assembly containing the embedded resources.</param>
    /// <param name="embeddedResourcePrefix">Prefix of resource names (dot separator).</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LocalizationResourceInfo AddJson(Assembly assembly, string embeddedResourcePrefix)
    {
        JsonSources.Add(new EmbeddedJsonSource(assembly, embeddedResourcePrefix));
        return this;
    }

    /// <summary>
    /// Adds parent resource types for translation inheritance.
    /// </summary>
    /// <param name="types">Types of the parent resources.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LocalizationResourceInfo AddBaseTypes(params ReadOnlySpan<Type> types)
    {
        BaseTypes.AddRange(types);
        return this;
    }
}
