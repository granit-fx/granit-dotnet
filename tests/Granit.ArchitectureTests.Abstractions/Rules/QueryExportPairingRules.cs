using System.Reflection;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable pairing rule (ADR-020): every entity with a <c>QueryDefinition&lt;T&gt;</c>
/// must also have an <c>ExportDefinition&lt;T&gt;</c>, and vice versa.
/// </summary>
/// <remarks>
/// The open generic base types are passed as parameters to avoid a hard dependency
/// on <c>Granit.QueryEngine</c> / <c>Granit.DataExchange.Export</c> in this package.
/// Pass <c>typeof(QueryDefinition&lt;&gt;)</c> and <c>typeof(ExportDefinition&lt;&gt;)</c>
/// from the test project, which already references those packages.
/// </remarks>
public static class QueryExportPairingRules
{
    /// <summary>
    /// Every entity that has a <c>QueryDefinition&lt;TEntity&gt;</c> subclass must also
    /// have an <c>ExportDefinition&lt;TEntity&gt;</c> subclass in the same codebase.
    /// </summary>
    /// <param name="callerAssembly">Test assembly — used to locate DLLs in the output directory.</param>
    /// <param name="assemblyGlob">Glob to select assemblies (e.g. <c>"Granit.*.dll"</c>).</param>
    /// <param name="queryDefinitionOpenType">
    /// Open generic base type for query definitions. Pass <c>typeof(QueryDefinition&lt;&gt;)</c>.
    /// </param>
    /// <param name="exportDefinitionOpenType">
    /// Open generic base type for export definitions. Pass <c>typeof(ExportDefinition&lt;&gt;)</c>.
    /// </param>
    /// <param name="exemptEntityFullNames">
    /// Fully-qualified entity type names that are intentionally exempt from the pairing rule.
    /// Each exemption should carry an inline comment with justification.
    /// </param>
    public static void EveryQueryDefinitionShouldHaveExportDefinition(
        Assembly callerAssembly,
        string assemblyGlob,
        Type queryDefinitionOpenType,
        Type exportDefinitionOpenType,
        IReadOnlySet<string>? exemptEntityFullNames = null)
    {
        exemptEntityFullNames ??= new HashSet<string>(StringComparer.Ordinal);

        (HashSet<Type> queryEntities, HashSet<Type> exportEntities) =
            ScanEntities(callerAssembly, assemblyGlob, queryDefinitionOpenType, exportDefinitionOpenType);

        IEnumerable<string> violations = queryEntities
            .Where(t => !exportEntities.Contains(t) && !exemptEntityFullNames.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        violations.ShouldBeEmpty(
            "ADR-020 pairing rule: every entity with a QueryDefinition must also have an ExportDefinition. " +
            "Add the matching ExportDefinition<T> in the module's Exports/ folder, " +
            "or add the entity to the exemption list with a justification. " +
            $"Unpaired: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Every entity that has an <c>ExportDefinition&lt;TEntity&gt;</c> subclass must also
    /// have a <c>QueryDefinition&lt;TEntity&gt;</c> subclass in the same codebase.
    /// </summary>
    public static void EveryExportDefinitionShouldHaveQueryDefinition(
        Assembly callerAssembly,
        string assemblyGlob,
        Type queryDefinitionOpenType,
        Type exportDefinitionOpenType,
        IReadOnlySet<string>? exemptEntityFullNames = null)
    {
        exemptEntityFullNames ??= new HashSet<string>(StringComparer.Ordinal);

        (HashSet<Type> queryEntities, HashSet<Type> exportEntities) =
            ScanEntities(callerAssembly, assemblyGlob, queryDefinitionOpenType, exportDefinitionOpenType);

        IEnumerable<string> violations = exportEntities
            .Where(t => !queryEntities.Contains(t) && !exemptEntityFullNames.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        violations.ShouldBeEmpty(
            "ADR-020 pairing rule: every entity with an ExportDefinition must also have a QueryDefinition. " +
            "Add the matching QueryDefinition<T> in the module's Queries/ folder, " +
            "or add the entity to the exemption list with a justification. " +
            $"Unpaired: {string.Join("; ", violations)}");
    }

    private static (HashSet<Type> queryEntities, HashSet<Type> exportEntities) ScanEntities(
        Assembly callerAssembly,
        string assemblyGlob,
        Type queryDefinitionOpenType,
        Type exportDefinitionOpenType)
    {
        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, assemblyGlob)
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(TryLoad)
            .Where(a => a is not null)
            .ToArray()!;

        HashSet<Type> queryEntities = [];
        HashSet<Type> exportEntities = [];

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.Where(t => t is not null)!]; }

            foreach (Type type in types.Where(t => !t.IsAbstract && t.IsClass))
            {
                Type? queryEntity = ExtractGenericArgument(type, queryDefinitionOpenType);
                if (queryEntity?.FullName is not null)
                {
                    queryEntities.Add(queryEntity);
                }

                Type? exportEntity = ExtractGenericArgument(type, exportDefinitionOpenType);
                if (exportEntity?.FullName is not null)
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

    private static Assembly? TryLoad(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
    }
}
