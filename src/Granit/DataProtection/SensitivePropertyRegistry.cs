using System.Collections.Frozen;
using System.Reflection;
using Granit.Reflection;

namespace Granit.DataProtection;

/// <summary>
/// Describes a sensitive property entry: classification level and protection mode.
/// </summary>
/// <param name="Level">ISO 27001 A.8.2 classification level.</param>
/// <param name="Mode">Protection mode when crossing a trust boundary.</param>
public readonly record struct SensitivePropertyEntry(Sensitivity Level, SensitiveDataMode Mode);

/// <summary>
/// Singleton registry of property names marked with <see cref="SensitiveDataAttribute"/>
/// across all loaded assemblies. Provides O(1) lookup by property name (case-insensitive).
/// </summary>
/// <remarks>
/// <para>
/// Consumers (MCP sanitizer, logging redactor, export filter) query this registry
/// to determine whether a given property name corresponds to a sensitive CLR property,
/// and which <see cref="Sensitivity"/> level and <see cref="SensitiveDataMode"/> apply.
/// </para>
/// <para>
/// When multiple types declare the same property name with different settings,
/// the most restrictive level and mode win.
/// </para>
/// </remarks>
public sealed class SensitivePropertyRegistry
{
    private readonly FrozenDictionary<string, SensitivePropertyEntry> _entries;

    /// <summary>
    /// Initializes the registry by scanning the given assemblies for
    /// <see cref="SensitiveDataAttribute"/> on public properties.
    /// </summary>
    public SensitivePropertyRegistry(IEnumerable<Assembly> assemblies)
    {
        Dictionary<string, SensitivePropertyEntry> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in assemblies)
        {
            ScanAssembly(assembly, map);
        }

        _entries = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> if the property name (case-insensitive) is marked as sensitive
    /// in any scanned type, and outputs the entry with level and mode.
    /// </summary>
    public bool TryGet(string propertyName, out SensitivePropertyEntry entry) =>
        _entries.TryGetValue(propertyName, out entry);

    /// <summary>
    /// Returns <c>true</c> if the property is sensitive at or above the given threshold.
    /// </summary>
    public bool IsSensitiveAtLevel(string propertyName, Sensitivity minimumLevel, out SensitivePropertyEntry entry)
    {
        if (_entries.TryGetValue(propertyName, out entry))
        {
            return entry.Level >= minimumLevel;
        }

        return false;
    }

    /// <summary>
    /// Returns all registered sensitive property names with their entries.
    /// </summary>
    public IReadOnlyDictionary<string, SensitivePropertyEntry> Entries => _entries;

    private static void ScanAssembly(Assembly assembly, Dictionary<string, SensitivePropertyEntry> map)
    {
        foreach (Type type in assembly.GetLoadableTypes())
        {
            ScanType(type, map);
        }
    }

    private static void ScanType(Type type, Dictionary<string, SensitivePropertyEntry> map)
    {
        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            SensitiveDataAttribute? attr = prop.GetCustomAttribute<SensitiveDataAttribute>();
            if (attr is null)
            {
                continue;
            }

            MergeEntry(map, prop.Name, new SensitivePropertyEntry(attr.Level, attr.Mode));
        }
    }

    private static void MergeEntry(
        Dictionary<string, SensitivePropertyEntry> map, string name, SensitivePropertyEntry incoming)
    {
        if (map.TryGetValue(name, out SensitivePropertyEntry existing))
        {
            // Most restrictive wins for both level and mode
            map[name] = new SensitivePropertyEntry(
                Level: (Sensitivity)Math.Max((int)incoming.Level, (int)existing.Level),
                Mode: (SensitiveDataMode)Math.Max((int)incoming.Mode, (int)existing.Mode));
        }
        else
        {
            map[name] = incoming;
        }
    }
}
