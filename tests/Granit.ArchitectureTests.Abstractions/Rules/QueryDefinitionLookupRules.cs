using System.Collections;
using System.Reflection;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable data-lookup coverage rule: every <b>filterable foreign-key column</b> declared on a
/// <c>QueryDefinition&lt;T&gt;</c> must attach a data-lookup source (<c>.Lookup("name")</c>) so the
/// admin-grid filter renders a typeahead picker instead of forcing the operator to type a raw
/// identifier (and so a stored value rehydrates into a human-readable label).
/// </summary>
/// <remarks>
/// <para>
/// A "foreign-key column" is detected heuristically by name: any column whose property name ends in
/// <c>"Id"</c> (other than the primary key <c>"Id"</c> itself) — e.g. <c>TenantId</c>, <c>PartyId</c>,
/// <c>PlanId</c>. This catches the overwhelming majority of links in the Granit codebases, where
/// foreign keys are consistently <c>{Entity}Id</c>.
/// </para>
/// <para>
/// Columns that legitimately have no lookup — polymorphic references (<c>EntityId</c> +
/// <c>EntityType</c>), opaque external identifiers (<c>ProviderTransactionId</c>), or plain
/// string identifiers that merely end in "Id" (<c>TaxId</c>) — are declared in the exemption set,
/// keyed as <c>"{EntityFullName}.{PropertyName}"</c>, each with an inline justification.
/// </para>
/// <para>
/// The open generic base type is passed as a parameter (pass <c>typeof(QueryDefinition&lt;&gt;)</c>)
/// to avoid a hard dependency on <c>Granit.QueryEngine</c> in this package; columns are read by
/// reflection over the public <c>GetColumns()</c> shape (<c>PropertyName</c>, <c>IsFilterable</c>,
/// <c>Lookup</c>).
/// </para>
/// </remarks>
public static class QueryDefinitionLookupRules
{
    /// <summary>
    /// Discovers every concrete <c>QueryDefinition&lt;T&gt;</c> in the assemblies matched by
    /// <paramref name="assemblyGlob"/> next to <paramref name="callerAssembly"/> and asserts that
    /// each filterable foreign-key column declares a lookup or is exempt.
    /// </summary>
    /// <param name="callerAssembly">Test assembly — used to locate DLLs in the output directory.</param>
    /// <param name="assemblyGlob">Glob to select assemblies (e.g. <c>"Granit.*.dll"</c>).</param>
    /// <param name="queryDefinitionOpenType">Open generic base. Pass <c>typeof(QueryDefinition&lt;&gt;)</c>.</param>
    /// <param name="exemptColumns">
    /// Columns intentionally exempt, keyed <c>"{EntityFullName}.{PropertyName}"</c>. Each should
    /// carry an inline justification.
    /// </param>
    public static void EveryFilterableForeignKeyColumnShouldDeclareLookup(
        Assembly callerAssembly,
        string assemblyGlob,
        Type queryDefinitionOpenType,
        IReadOnlySet<string>? exemptColumns = null)
    {
        ArgumentNullException.ThrowIfNull(callerAssembly);
        ArgumentNullException.ThrowIfNull(queryDefinitionOpenType);

        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        List<Type> definitions =
        [
            .. Directory.GetFiles(outputDir, assemblyGlob)
                .Where(path =>
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    return !name.Contains("Tests", StringComparison.Ordinal)
                        && !name.EndsWith(".resources", StringComparison.Ordinal);
                })
                .Select(TryLoad)
                .Where(a => a is not null)
                .SelectMany(GetLoadableTypes!)
                .Where(t => IsConcreteDefinition(t, queryDefinitionOpenType)),
        ];

        // Discovery tripwire: a glob typo, a renamed base type, or a signature drift would otherwise
        // leave the rule silently green over zero definitions. Fail loudly instead. The codebase
        // always ships concrete QueryDefinition<T> types, so an empty set means the rule is broken,
        // not that coverage is complete.
        definitions.ShouldNotBeEmpty(
            $"Data-lookup coverage rule discovered no concrete {queryDefinitionOpenType.Name} types " +
            $"under '{assemblyGlob}' next to {callerAssembly.GetName().Name}. The rule is mis-wired " +
            "(wrong glob, wrong open generic, or definitions that no longer derive from it) — fix the " +
            "wiring rather than assuming there is nothing to check.");

        EveryFilterableForeignKeyColumnShouldDeclareLookup(definitions, queryDefinitionOpenType, exemptColumns);
    }

    /// <summary>
    /// Pure overload over an explicit candidate type set — instantiates each
    /// <c>QueryDefinition&lt;T&gt;</c>, reads its columns, and asserts lookup coverage. Useful in
    /// unit tests with synthetic definitions.
    /// </summary>
    public static void EveryFilterableForeignKeyColumnShouldDeclareLookup(
        IEnumerable<Type> candidateTypes,
        Type queryDefinitionOpenType,
        IReadOnlySet<string>? exemptColumns = null)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);
        ArgumentNullException.ThrowIfNull(queryDefinitionOpenType);
        exemptColumns ??= new HashSet<string>(StringComparer.Ordinal);

        List<string> violations = [];

        foreach (Type type in candidateTypes)
        {
            if (!IsConcreteDefinition(type, queryDefinitionOpenType))
            {
                continue;
            }

            Type? entity = ExtractGenericArgument(type, queryDefinitionOpenType);
            if (entity?.FullName is null)
            {
                continue;
            }

            object? instance = TryInstantiate(type);
            if (instance is null)
            {
                // Definitions without a parameterless constructor (e.g. dynamically parameterised
                // reference-data definitions) cannot be introspected statically — skip them.
                continue;
            }

            IEnumerable? columns = TryGetColumns(instance);
            if (columns is null)
            {
                continue;
            }

            foreach (object? column in columns)
            {
                if (column is null)
                {
                    continue;
                }

                string? propertyName = GetProperty(column, "PropertyName") as string;
                if (propertyName is null || !IsForeignKeyColumn(propertyName))
                {
                    continue;
                }

                if (GetProperty(column, "IsFilterable") is not true)
                {
                    continue;
                }

                if (GetProperty(column, "Lookup") is not null)
                {
                    continue;
                }

                string key = $"{entity.FullName}.{propertyName}";
                if (!exemptColumns.Contains(key))
                {
                    violations.Add(key);
                }
            }
        }

        violations.Sort(StringComparer.Ordinal);

        violations.ShouldBeEmpty(
            "Data-lookup coverage rule: every filterable foreign-key column (a '*Id' column) must declare " +
            ".Lookup(\"<source>\") so the admin-grid filter renders a typeahead picker instead of a raw " +
            "identifier. Wire the column to a registered lookup source, or add it to the exemption list " +
            "with a justification (polymorphic / opaque / external identifiers). " +
            $"Unwired: {string.Join("; ", violations)}");
    }

    private static bool IsConcreteDefinition(Type type, Type queryDefinitionOpenType) =>
        type is { IsAbstract: false, IsClass: true }
        && !type.IsGenericTypeDefinition
        && ExtractGenericArgument(type, queryDefinitionOpenType) is not null;

    private static bool IsForeignKeyColumn(string propertyName) =>
        propertyName.Length > "Id".Length
        && propertyName.EndsWith("Id", StringComparison.Ordinal);

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

    private static object? TryInstantiate(Type type)
    {
        try
        {
            return Activator.CreateInstance(type, nonPublic: true);
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException)
        {
            return null;
        }
    }

    private static IEnumerable? TryGetColumns(object instance)
    {
        try
        {
            MethodInfo? method = instance.GetType().GetMethod("GetColumns", Type.EmptyTypes);
            return method?.Invoke(instance, null) as IEnumerable;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static object? GetProperty(object obj, string name) =>
        obj.GetType().GetProperty(name)?.GetValue(obj);

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.Where(t => t is not null)!];
        }
    }

    private static Assembly? TryLoad(string path)
    {
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            return null;
        }
    }
}
