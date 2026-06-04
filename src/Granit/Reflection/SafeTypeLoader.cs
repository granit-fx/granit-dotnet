using System.Reflection;

namespace Granit.Reflection;

/// <summary>
/// Resilient assembly type enumeration for discovery scans. <see cref="Assembly.GetTypes"/> and
/// <see cref="Assembly.GetExportedTypes"/> throw when an assembly's surface references a type from
/// a dependency that is not loaded: <see cref="ReflectionTypeLoadException"/> for non-public types,
/// but <see cref="FileNotFoundException"/> / <see cref="TypeLoadException"/> for the public surface
/// (e.g. a module exporting Wolverine-derived types when <c>Wolverine.dll</c> did not flow). These
/// helpers return the loadable subset instead of letting one such assembly take down an unrelated
/// "scan every loaded assembly" discovery site.
/// </summary>
public static class SafeTypeLoader
{
    /// <summary>
    /// Returns the loadable types of <paramref name="assembly"/> (public and non-public), never throwing
    /// for missing-dependency failures. Dynamic assemblies return empty.
    /// </summary>
    public static IReadOnlyList<Type> GetLoadableTypes(this Assembly assembly) =>
        Load(assembly, static a => a.GetTypes());

    /// <summary>
    /// Returns the loadable <em>exported</em> (public) types of <paramref name="assembly"/>, never throwing
    /// for missing-dependency failures. Dynamic assemblies return empty.
    /// </summary>
    public static IReadOnlyList<Type> GetLoadableExportedTypes(this Assembly assembly) =>
        Load(assembly, static a => a.GetExportedTypes());

    /// <summary>
    /// Shared resilience core. On <see cref="ReflectionTypeLoadException"/> returns the partial set that
    /// did load; on a missing-dependency load failure returns empty; any other exception propagates.
    /// </summary>
    internal static IReadOnlyList<Type> Load(Assembly assembly, Func<Assembly, Type[]> getTypes)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly.IsDynamic)
        {
            return [];
        }

        try
        {
            return getTypes(assembly);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Keep the types that did load; the rest reference an unresolved dependency.
            return [.. ex.Types.OfType<Type>()];
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            // The assembly's public surface references a type from a dependency that is not present.
            return [];
        }
    }
}
