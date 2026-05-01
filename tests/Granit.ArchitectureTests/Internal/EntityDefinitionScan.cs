using System.Reflection;
using Granit.Entities;
using Granit.QueryEngine;

namespace Granit.ArchitectureTests.Internal;

/// <summary>
/// Reflection helpers shared by every <c>EntityDefinition</c> ↔ <c>QueryDefinition</c>
/// pairing test (kanban / calendar / gallery / future view kinds). Each test
/// indexes <see cref="EntityDefinition{TEntity}"/> instances by their generic
/// <c>TEntity</c> argument and cross-checks the layout's surfaced properties
/// against <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c> — the helpers
/// here factor out the boilerplate so each test stays focused on its
/// layout-specific assertion.
/// </summary>
internal static class EntityDefinitionScan
{
    public static List<IEntityDefinitionDescriptor> ScanEntityDefinitions()
    {
        Type baseType = typeof(EntityDefinition<>);
        List<IEntityDefinitionDescriptor> instances = [];

        foreach (Assembly assembly in LoadGranitAssemblies())
        {
            foreach (Type type in SafeGetTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (!InheritsFromOpenGeneric(type, baseType))
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IEntityDefinitionDescriptor instance)
                {
                    instances.Add(instance);
                }
            }
        }

        return instances;
    }

    public static Dictionary<Type, IQueryDefinitionDescriptor> ScanQueryDefinitions()
    {
        Type baseType = typeof(QueryDefinition<>);
        Dictionary<Type, IQueryDefinitionDescriptor> byEntityType = [];

        foreach (Assembly assembly in LoadGranitAssemblies())
        {
            foreach (Type type in SafeGetTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (!InheritsFromOpenGeneric(type, baseType))
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IQueryDefinitionDescriptor instance)
                {
                    byEntityType[instance.EntityType] = instance;
                }
            }
        }

        return byEntityType;
    }

    /// <summary>
    /// Reads <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c> via reflection — the
    /// method lives on the generic base, not on <see cref="IQueryDefinitionDescriptor"/>,
    /// so a static cast isn't enough.
    /// </summary>
    public static HashSet<string> ReadColumnPropertyNames(IQueryDefinitionDescriptor query)
    {
        MethodInfo? getColumns = query.GetType().GetMethod(
            "GetColumns",
            BindingFlags.Instance | BindingFlags.Public,
            Type.EmptyTypes);

        if (getColumns is null)
        {
            return [];
        }

        if (getColumns.Invoke(query, null) is not System.Collections.IEnumerable columns)
        {
            return [];
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (object? column in columns)
        {
            if (column is null) { continue; }
            string? propertyName = column.GetType()
                .GetProperty("PropertyName", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(column) as string;
            if (propertyName is { Length: > 0 })
            {
                names.Add(propertyName);
            }
        }

        return names;
    }

    private static IEnumerable<Assembly> LoadGranitAssemblies()
    {
        string outputDir = Path.GetDirectoryName(typeof(EntityDefinitionScan).Assembly.Location)!;

        return Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
            })
            .Where(a => a is not null)!;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    private static bool InheritsFromOpenGeneric(Type candidate, Type openGenericBase)
    {
        Type? cursor = candidate.BaseType;
        while (cursor is not null && cursor != typeof(object))
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return true;
            }
            cursor = cursor.BaseType;
        }
        return false;
    }
}
