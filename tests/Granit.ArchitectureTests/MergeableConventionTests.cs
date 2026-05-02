using System.Reflection;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Cross-module convention checks for the Party merge framework.
/// </summary>
/// <remarks>
/// <para>
/// The merge orchestrator (<c>IMergeService&lt;Party&gt;</c>) discovers cross-module
/// rewriters via DI registration as <see cref="IReferenceRewriter{Party}"/>. If a module
/// holds a persisted <see cref="PartyId"/> on one of its aggregate roots but never ships
/// a rewriter, every <c>Party.Merge</c> against that aggregate type silently leaves
/// orphan references behind. The asserts below catch that drift at build time rather
/// than at the next production merge.
/// </para>
/// </remarks>
public sealed class MergeableConventionTests
{
    /// <summary>
    /// Modules that hold a persisted <see cref="PartyId"/> on an aggregate root and
    /// therefore MUST register a corresponding <see cref="IReferenceRewriter{Party}"/>.
    /// Drift detected via reflection in
    /// <see cref="Every_aggregate_with_a_persisted_PartyId_outside_Granit_Parties_has_a_rewriter"/>;
    /// this list is the curated whitelist of expected modules to keep the failure message
    /// actionable.
    /// </summary>
    private static readonly string[] ExpectedRewriterAssemblyNames =
    [
        // Granit.Parties owns the aggregate; PartyParentReferenceRewriter +
        // PartyChildrenReferenceRewriter live in Granit.Parties.Mergeable.
        "Granit.Parties.Mergeable",
        // Cross-module rewriters live next to their owning DbContext (inlined approach,
        // see PRs #1358 / #1380 / #1403 — no dedicated *.Mergeable package per module).
        "Granit.Invoicing.EntityFrameworkCore",
        "Granit.Subscriptions.EntityFrameworkCore",
        "Granit.CustomerBalance.EntityFrameworkCore",
    ];

    [Fact]
    public void Every_module_in_the_expected_list_actually_ships_a_rewriter()
    {
        IReadOnlyList<Type> rewriters = FindAllReferenceRewriterImplementations();
        var assembliesShippingRewriters = rewriters
            .Select(t => t.Assembly.GetName().Name!)
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<string> missing = ExpectedRewriterAssemblyNames
            .Where(expected => !assembliesShippingRewriters.Contains(expected));

        missing.ShouldBeEmpty(
            "Every assembly in MergeableConventionTests.ExpectedRewriterAssemblyNames must ship at " +
            "least one IReferenceRewriter<Party> implementation. Missing: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void Every_aggregate_with_a_persisted_PartyId_outside_Granit_Parties_has_a_rewriter()
    {
        IReadOnlyList<Type> rewriters = FindAllReferenceRewriterImplementations();
        var rewriterAssemblies = rewriters
            .Select(t => t.Assembly.GetName().Name!)
            .Select(StripEntityFrameworkCoreSuffix)
            .ToHashSet(StringComparer.Ordinal);

        // Discover aggregate roots (any concrete Entity subclass) that expose a PartyId
        // property — that's our heuristic for "the row carries a persisted Party FK".
        var aggregatesWithPartyId = AllSrcTypes()
            .Where(t => !t.IsAbstract && typeof(Entity).IsAssignableFrom(t))
            .Where(t => t.GetProperty("PartyId", BindingFlags.Public | BindingFlags.Instance)?
                            .PropertyType == typeof(PartyId))
            .Where(t => t.Assembly.GetName().Name != "Granit.Parties") // self-FK is parent reparenting; covered separately
            .ToList();

        List<string> missing = [];
        foreach (Type aggregate in aggregatesWithPartyId)
        {
            string aggregateAssembly = StripEntityFrameworkCoreSuffix(aggregate.Assembly.GetName().Name!);
            if (!rewriterAssemblies.Contains(aggregateAssembly))
            {
                missing.Add($"{aggregate.FullName} (in {aggregate.Assembly.GetName().Name}) — " +
                            $"no IReferenceRewriter<Party> found in '{aggregateAssembly}.EntityFrameworkCore' or '{aggregateAssembly}.Mergeable'.");
            }
        }

        missing.ShouldBeEmpty(
            "Every aggregate that persists a PartyId outside Granit.Parties MUST ship a " +
            "corresponding IReferenceRewriter<Party> implementation, or the merge orchestrator " +
            "will silently leave orphan references on that table. Missing rewriters:\n  - "
            + string.Join("\n  - ", missing));
    }

    [Fact]
    public void Every_IReferenceRewriter_Party_implementation_lives_in_a_persistence_or_mergeable_package()
    {
        IReadOnlyList<Type> rewriters = FindAllReferenceRewriterImplementations();

        List<string> misplaced = [];
        foreach (Type rewriter in rewriters)
        {
            string asm = rewriter.Assembly.GetName().Name!;
            // Rewriters either live in *.Mergeable (the central Parties case) or
            // *.EntityFrameworkCore (cross-module inlined — see PRs #1358 / #1380 / #1403).
            // Anywhere else is a smell: rewriter is a SQL/persistence concern that belongs
            // next to the DbContext that owns the foreign key.
            bool ok = asm.EndsWith(".Mergeable", StringComparison.Ordinal)
                   || asm.EndsWith(".EntityFrameworkCore", StringComparison.Ordinal);

            if (!ok)
            {
                misplaced.Add($"{rewriter.FullName} (in {asm})");
            }
        }

        misplaced.ShouldBeEmpty(
            "Every IReferenceRewriter<Party> implementation must live in a *.EntityFrameworkCore " +
            "or *.Mergeable assembly — keeps the SQL/persistence concern next to the DbContext " +
            "that owns the foreign key. Misplaced rewriters:\n  - "
            + string.Join("\n  - ", misplaced));
    }

    private static List<Type> FindAllReferenceRewriterImplementations() =>
        [.. AllSrcTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IReferenceRewriter<>)
                && i.GenericTypeArguments[0] == typeof(Party)))];

    private static readonly Lock EagerLoadGate = new();
    private static bool _eagerLoaded;

    /// <summary>
    /// Eagerly loads every <c>Granit.*.dll</c> sitting next to the test assembly so
    /// reflection-based type discovery sees them. Without this, the .NET runtime would
    /// only resolve assemblies lazily on first type-touch, and rewriters living in
    /// otherwise-untouched assemblies (e.g. <c>Granit.Parties.Mergeable</c>) would be
    /// invisible to <see cref="AppDomain.GetAssemblies"/>.
    /// </summary>
    private static void EnsureGranitAssembliesLoaded()
    {
        lock (EagerLoadGate)
        {
            if (_eagerLoaded)
            {
                return;
            }
            string baseDir = AppContext.BaseDirectory;
            foreach (string dll in Directory.EnumerateFiles(baseDir, "Granit.*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(dll);
                    Assembly.Load(name);
                }
                catch (BadImageFormatException) { /* native or analyzer assembly — skip */ }
                catch (FileLoadException) { /* already loaded — skip */ }
            }
            _eagerLoaded = true;
        }
    }

    private static IEnumerable<Type> AllSrcTypes()
    {
        EnsureGranitAssembliesLoaded();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Granit", StringComparison.Ordinal))
            {
                continue;
            }
            // Skip test assemblies that happen to start with "Granit".
            if (name.EndsWith(".Tests", StringComparison.Ordinal)
                || name.Contains(".Tests.", StringComparison.Ordinal)
                || name.EndsWith(".Tests.Integration", StringComparison.Ordinal))
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
            }

            foreach (Type t in types)
            {
                yield return t;
            }
        }
    }

    /// <summary>
    /// Maps an EF Core or Mergeable assembly name to its base module name so we can
    /// compare against an aggregate's home assembly. Examples:
    /// <list type="bullet">
    /// <item><c>Granit.Invoicing.EntityFrameworkCore</c> → <c>Granit.Invoicing</c></item>
    /// <item><c>Granit.Parties.Mergeable</c> → <c>Granit.Parties</c></item>
    /// </list>
    /// </summary>
    private static string StripEntityFrameworkCoreSuffix(string assemblyName)
    {
        if (assemblyName.EndsWith(".EntityFrameworkCore", StringComparison.Ordinal))
        {
            return assemblyName[..^".EntityFrameworkCore".Length];
        }
        if (assemblyName.EndsWith(".Mergeable", StringComparison.Ordinal))
        {
            return assemblyName[..^".Mergeable".Length];
        }
        return assemblyName;
    }
}
