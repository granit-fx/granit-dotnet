using System.Reflection;
using Granit.Entities;
using Granit.Entities.Forms;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Closes the Phase 2.A.2 deferred guard: every property a kanban card surfaces
/// (<c>Card.TitleProperty</c> + <c>Card.Fields[*].PropertyName</c>) must appear
/// in the matching <c>QueryDefinition&lt;TEntity&gt;.GetColumns()</c> whitelist.
/// </summary>
/// <remarks>
/// <para>
/// The kanban tile reads its rows from the entity's <c>QueryDefinition</c> projection.
/// Card fields outside the whitelist either render blank (the SELECT does not include
/// the column) or — worse — leak a property the security model intentionally hid from
/// the list endpoint. Either way the host has a bug; this test fails fast at boot
/// instead of letting the regression reach production.
/// </para>
/// <para>
/// Reflection scans every <c>Granit.*.dll</c> in the test output directory and indexes
/// concrete <see cref="EntityDefinition{TEntity}"/> subclasses by their generic
/// <c>TEntity</c> argument. For each entity that declares a kanban layout, the test
/// finds the matching <see cref="QueryDefinition{TEntity}"/> and cross-checks the
/// card's property names against <c>GetColumns()</c>.
/// </para>
/// <para>
/// An entity with a kanban layout but NO <c>QueryDefinition</c> at all is a separate
/// hard failure (the kanban can't render) — also reported by this test.
/// </para>
/// </remarks>
public sealed class KanbanCardWhitelistPairingTests
{
    [Fact]
    public void Every_kanban_card_property_must_be_whitelisted_in_the_matching_QueryDefinition()
    {
        List<IEntityDefinitionDescriptor> entities = ScanEntityDefinitions();
        Dictionary<Type, IQueryDefinitionDescriptor> queriesByEntityType = ScanQueryDefinitions();

        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor entity in entities)
        {
            EntityDefinitionDescriptor descriptor = entity.Descriptor;
            KanbanLayoutDescriptor? kanban = descriptor.ListLayouts
                .OfType<KanbanLayoutDescriptor>()
                .FirstOrDefault();

            if (kanban is null)
            {
                continue;
            }

            if (!queriesByEntityType.TryGetValue(descriptor.EntityType, out IQueryDefinitionDescriptor? query))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' declares a KanbanView but no matching QueryDefinition<{descriptor.EntityType.Name}> was found. "
                    + "The kanban tile reads its rows from the entity's QueryDefinition projection — without one, the board can't render. "
                    + "Add a QueryDefinition<T> in the same module's Queries/ folder and register it via AddQueryDefinition<T, TDefinition>().");
                continue;
            }

            HashSet<string> whitelistedProperties = ReadColumnPropertyNames(query);

            if (kanban.Card.TitleProperty is { } title && !whitelistedProperties.Contains(title))
            {
                violations.Add(
                    $"Entity '{descriptor.Name}' kanban Card.Title='{title}' is not in the QueryDefinition column whitelist. "
                    + $"Add `.Column(e => e.{title})` in the matching QueryDefinition or change the kanban Title to a whitelisted property.");
            }

            foreach (FieldDescriptor field in kanban.Card.Fields)
            {
                if (!whitelistedProperties.Contains(field.PropertyName))
                {
                    violations.Add(
                        $"Entity '{descriptor.Name}' kanban Card.Field '{field.PropertyName}' is not in the QueryDefinition column whitelist. "
                        + $"Add `.Column(e => e.{field.PropertyName})` in the matching QueryDefinition or remove the field from the card.");
                }
            }
        }

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    private static List<IEntityDefinitionDescriptor> ScanEntityDefinitions()
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

    private static Dictionary<Type, IQueryDefinitionDescriptor> ScanQueryDefinitions()
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
    private static HashSet<string> ReadColumnPropertyNames(IQueryDefinitionDescriptor query)
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
        string outputDir = Path.GetDirectoryName(typeof(KanbanCardWhitelistPairingTests).Assembly.Location)!;

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
