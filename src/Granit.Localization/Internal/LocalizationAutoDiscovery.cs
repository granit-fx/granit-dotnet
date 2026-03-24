// ---------------------------------------------------------------------------
// LocalizationAutoDiscovery.cs
// Scans loaded assemblies to automatically detect JSON localization resources
// by naming convention.
//
// Convention : {anything}.Localization.{ResourceName}.{culture}.json
// Example    : Granit.Vault.Localization.Vault.fr.json
//
// Triggered when GranitLocalizationOptions.EnableAutoDiscovery = true.
// Resources already registered explicitly are not overwritten.
// ---------------------------------------------------------------------------

using System.Reflection;
using Granit.Localization;
using Granit.Localization.Options;

namespace Granit.Localization.Internal;

/// <summary>
/// Automatic discovery of JSON localization resources by convention.
/// </summary>
internal static class LocalizationAutoDiscovery
{
    private const string LocalizationSegment = ".Localization.";

    /// <summary>
    /// Scans all assemblies loaded in the AppDomain and registers
    /// localization resources discovered by convention.
    /// </summary>
    /// <param name="options">Localization options to enrich.</param>
    public static void Discover(GranitLocalizationOptions options)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            DiscoverFromAssembly(assembly, options);
        }
    }

    private static void DiscoverFromAssembly(Assembly assembly, GranitLocalizationOptions options)
    {
        string[] resourceNames;
        try
        {
            resourceNames = assembly.GetManifestResourceNames();
        }
        catch (NotSupportedException)
        {
            return;
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }

        foreach (Type type in types)
        {
            LocalizationResourceNameAttribute? attr =
                type.GetCustomAttribute<LocalizationResourceNameAttribute>();

            if (attr is null)
            {
                continue;
            }

            // Do not overwrite an explicit registration
            if (options.Resources.TryGetValue(type, out _))
            {
                continue;
            }

            // Look for the JSON prefix: *.Localization.{Name}.{culture}.json
            string pattern = $"{LocalizationSegment}{attr.Name}.";
            string? prefix = FindPrefix(resourceNames, pattern);

            if (prefix is null)
            {
                continue;
            }

            // Detect parent types via [InheritResource]
            var inheritAttrs =
                (InheritResourceAttribute[])type.GetCustomAttributes(
                    typeof(InheritResourceAttribute), inherit: true);

            Type[] baseTypes = [.. inheritAttrs.SelectMany(a => a.BaseResourceTypes)];

            LocalizationResourceInfo info = options.Resources
                .Add(type, attr.DefaultCulture)
                .AddJson(assembly, prefix);

            if (baseTypes.Length > 0)
            {
                info.AddBaseTypes(baseTypes);
            }
        }
    }

    /// <summary>
    /// Returns the embedded resource prefix matching the given pattern,
    /// or null if no JSON file matches.
    /// </summary>
    private static string? FindPrefix(string[] resourceNames, string pattern)
    {
        foreach (string resourceName in resourceNames)
        {
            if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int idx = resourceName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                continue;
            }

            // prefix = everything up to the end of "{Name}" (without the trailing "." of the pattern)
            return resourceName[..(idx + pattern.Length - 1)];
        }

        return null;
    }
}
