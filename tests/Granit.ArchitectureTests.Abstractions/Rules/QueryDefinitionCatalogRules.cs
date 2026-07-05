using System.Reflection;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Query-catalog completeness guard. A <c>QueryDefinition&lt;T&gt;</c> reaches
/// <c>IQueryDefinitionRegistry</c> — and therefore <c>GET /catalog</c> and the dashboard-widget
/// query picker — only if it is registered as a non-keyed <c>IQueryDefinitionDescriptor</c>. The
/// standard <c>AddQueryDefinition&lt;TEntity, TDefinition&gt;()</c> helper does that binding for
/// free, but it is constrained to <c>new()</c>: a definition <b>without a public parameterless
/// constructor</b> cannot use it and must be wired by hand, where the descriptor binding is easy to
/// drop (the type then silently vanishes from the catalog). This rule forces every such bespoke
/// definition through a reviewed exemption list, keeping the manual binding on the reviewer's radar.
/// </summary>
/// <remarks>
/// <para>
/// The rule is deliberately <i>structural</i>: it flags the bespoke registration <i>path</i>, not a
/// proven-missing binding — proving the binding requires a DI-resolution test against the owning
/// module. The two form a defence in depth: this guard makes hand-wired definitions visible; the
/// module's DI test proves each one actually resolves as a descriptor.
/// </para>
/// <para>
/// The open generic base is passed as a parameter (pass <c>typeof(QueryDefinition&lt;&gt;)</c>) so
/// this package takes no dependency on <c>Granit.QueryEngine</c>. Discovery runs by reflection over
/// the assemblies matched by the glob, so downstream repos reuse the rule against their own
/// assemblies; each supplies its own exemption set.
/// </para>
/// </remarks>
public static class QueryDefinitionCatalogRules
{
    /// <summary>
    /// Discovers every concrete <c>QueryDefinition&lt;T&gt;</c> in the assemblies matched by
    /// <paramref name="assemblyGlob"/> next to <paramref name="callerAssembly"/> and asserts that
    /// each one lacking a public parameterless constructor (i.e. registered by hand rather than via
    /// the <c>new()</c>-constrained helper) is listed in <paramref name="exemptDefinitions"/>.
    /// </summary>
    /// <param name="callerAssembly">Test assembly — used to locate DLLs in the output directory.</param>
    /// <param name="assemblyGlob">Glob to select assemblies (e.g. <c>"Granit.*.dll"</c>).</param>
    /// <param name="queryDefinitionOpenType">Open generic base. Pass <c>typeof(QueryDefinition&lt;&gt;)</c>.</param>
    /// <param name="exemptDefinitions">
    /// Bespoke definitions intentionally hand-registered, keyed by <c>Type.FullName</c>. Each should
    /// carry an inline justification and be covered by a DI-resolution test proving the descriptor binding.
    /// </param>
    public static void EveryBespokeDefinitionIsExempt(
        Assembly callerAssembly,
        string assemblyGlob,
        Type queryDefinitionOpenType,
        IReadOnlySet<string>? exemptDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(callerAssembly);
        ArgumentException.ThrowIfNullOrEmpty(assemblyGlob);
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
        // leave the rule silently green over zero definitions. The codebase always ships concrete
        // QueryDefinition<T> types, so an empty set means the rule is broken, not that there is
        // nothing to check.
        definitions.ShouldNotBeEmpty(
            $"Query-catalog guard discovered no concrete {queryDefinitionOpenType.Name} types under " +
            $"'{assemblyGlob}' next to {callerAssembly.GetName().Name}. The rule is mis-wired (wrong glob " +
            "or wrong open generic) — fix the wiring rather than assuming there is nothing to check.");

        EveryBespokeDefinitionIsExempt(definitions, queryDefinitionOpenType, exemptDefinitions);
    }

    /// <summary>
    /// Pure overload over an explicit candidate type set — asserts every concrete
    /// <c>QueryDefinition&lt;T&gt;</c> without a public parameterless constructor is exempt. Useful
    /// in unit tests with synthetic definitions.
    /// </summary>
    public static void EveryBespokeDefinitionIsExempt(
        IEnumerable<Type> candidateTypes,
        Type queryDefinitionOpenType,
        IReadOnlySet<string>? exemptDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);
        ArgumentNullException.ThrowIfNull(queryDefinitionOpenType);
        exemptDefinitions ??= new HashSet<string>(StringComparer.Ordinal);

        List<string> violations =
        [
            .. candidateTypes
                .Where(t => IsConcreteDefinition(t, queryDefinitionOpenType) && !HasPublicParameterlessConstructor(t))
                .Select(t => t.FullName!)
                .Where(name => !exemptDefinitions.Contains(name)),
        ];

        violations.Sort(StringComparer.Ordinal);

        violations.ShouldBeEmpty(
            "Query-catalog guard: every concrete QueryDefinition<T> without a public parameterless " +
            "constructor is registered by hand — it cannot use AddQueryDefinition<TEntity, TDefinition>(), " +
            "which is constrained to new(). Hand-wiring MUST also bind IQueryDefinitionDescriptor, otherwise " +
            "the type never reaches IQueryDefinitionRegistry / GET /catalog / the dashboard-widget query " +
            "picker. Confirm the descriptor binding (cover it with a DI-resolution test) and add the type to " +
            "the exemption list with a justification. " +
            $"Bespoke and unexempt: {string.Join("; ", violations)}");
    }

    private static bool IsConcreteDefinition(Type type, Type queryDefinitionOpenType) =>
        type is { IsAbstract: false, IsClass: true }
        && !type.IsGenericTypeDefinition
        && DerivesFromOpenGeneric(type, queryDefinitionOpenType);

    private static bool HasPublicParameterlessConstructor(Type type) =>
        type.GetConstructor(Type.EmptyTypes) is not null;

    private static bool DerivesFromOpenGeneric(Type candidate, Type openGenericBase)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return true;
            }
        }

        return false;
    }

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
