using System.Reflection;
using Granit.DataExchange.Export;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the pairing rule from ADR-020 — every admin-visible entity that has a
/// <see cref="QueryDefinition{TEntity}"/> must also have an <see cref="ExportDefinition{TEntity}"/>,
/// and vice versa. Queries and exports are two facets of the same admin-grid use case
/// (browse + export); declaring one without the other leaves the admin UX
/// half-implemented.
/// </summary>
/// <remarks>
/// Reflection scans every <c>Granit.*.dll</c> in the test output directory and indexes
/// concrete <c>QueryDefinition&lt;T&gt;</c> / <c>ExportDefinition&lt;T&gt;</c> subclasses
/// by their generic <c>TEntity</c> argument. Any entity that appears in only one of the
/// two indexes is reported.
/// </remarks>
public sealed class QueryExportPairingTests
{
    /// <summary>
    /// Entities deliberately exempted from the pairing rule. Each entry MUST be justified.
    /// Pure infrastructure entities (internal cache rows, audit log details, internal
    /// config state) that are not exposed in an admin grid use the reflection-based
    /// fallback <c>ReflectionExportDefinition</c> and do not need a paired Query.
    /// </summary>
    private static readonly HashSet<string> PairingExemptions = new(StringComparer.Ordinal)
    {
        // Add justified exemptions here, e.g.:
        // "Granit.AI.Domain.AIWorkspaceEntity",  // internal workspace state, never admin-listed
    };

    [Fact]
    public void Every_QueryDefinition_should_have_a_matching_ExportDefinition()
    {
        (HashSet<Type> queryEntities, HashSet<Type> exportEntities) = ScanEntities();

        IEnumerable<string> queryWithoutExport = queryEntities
            .Where(t => !exportEntities.Contains(t) && !PairingExemptions.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        queryWithoutExport.ShouldBeEmpty(
            "ADR-020 pairing rule: every entity with a QueryDefinition must also have an ExportDefinition. " +
            "Add the matching `ExportDefinition<T>` in the same module's `Exports/` folder, " +
            "register it via `services.AddExportDefinition<T, TDefinition>()`, " +
            "or add the entity to PairingExemptions with a justification.");
    }

    [Fact]
    public void Every_ExportDefinition_should_have_a_matching_QueryDefinition()
    {
        (HashSet<Type> queryEntities, HashSet<Type> exportEntities) = ScanEntities();

        IEnumerable<string> exportWithoutQuery = exportEntities
            .Where(t => !queryEntities.Contains(t) && !PairingExemptions.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        exportWithoutQuery.ShouldBeEmpty(
            "ADR-020 pairing rule: every entity with an ExportDefinition must also have a QueryDefinition. " +
            "Add the matching `QueryDefinition<T>` in the same module's `Queries/` folder, " +
            "register it via `services.AddQueryDefinition<T, TDefinition>()`, " +
            "or add the entity to PairingExemptions with a justification.");
    }

    private static (HashSet<Type> queryEntities, HashSet<Type> exportEntities) ScanEntities()
    {
        string outputDir = Path.GetDirectoryName(typeof(QueryExportPairingTests).Assembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
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
            .Where(a => a is not null)
            .ToArray()!;

        HashSet<Type> queryEntities = [];
        HashSet<Type> exportEntities = [];

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.Where(t => t is not null)!]; }

            foreach (Type type in types)
            {
                if (type.IsAbstract || !type.IsClass)
                {
                    continue;
                }

                Type? queryEntity = ExtractGenericArgument(type, typeof(QueryDefinition<>));
                if (queryEntity is not null && queryEntity.FullName is not null)
                {
                    queryEntities.Add(queryEntity);
                }

                Type? exportEntity = ExtractGenericArgument(type, typeof(ExportDefinition<>));
                if (exportEntity is not null && exportEntity.FullName is not null)
                {
                    exportEntities.Add(exportEntity);
                }
            }
        }

        return (queryEntities, exportEntities);
    }

    private static Type? ExtractGenericArgument(Type candidate, Type openGenericBase)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return cursor.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
