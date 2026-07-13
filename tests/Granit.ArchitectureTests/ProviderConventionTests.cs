using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Locks the canonical notification-provider template (reference implementation:
/// <c>Granit.Notifications.MobilePush.AwsSns</c>). A provider package — any
/// <c>Granit.Notifications.*</c> assembly implementing a channel <c>I*Sender</c>
/// interface — must ship:
/// <list type="bullet">
///   <item>an options class with a <c>public const string SectionName</c>;</item>
///   <item>effective options validation (<c>AddGranitProviderOptions</c>, or
///         <c>ValidateDataAnnotations()</c>, or an <c>IValidateOptions&lt;&gt;</c>);</item>
///   <item>a <c>SectionName</c> assertion in its test project (renames must surface in CI);</item>
///   <item>a registered <c>*ActivitySource</c>;</item>
///   <item>a health check;</item>
///   <item>a self-registering module (<c>ConfigureServices</c> override) whose
///         <c>[DependsOn]</c> covers every direct project reference carrying a module;</item>
///   <item>a top-level package name — providers never nest under a channel
///         (<c>Granit.Notifications.Twilio</c>, not <c>Granit.Notifications.Sms.Twilio</c>).</item>
/// </list>
/// Grandfathered violations live in <see cref="ProviderExemptions"/> with one justification
/// per entry; the stale-exemption fact guarantees the list only shrinks.
/// </summary>
public sealed partial class ProviderConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    // Lazy: static initializers run in textual order, and discovery depends on
    // LoadedGranitAssemblies declared further down the file.
    private static readonly Lazy<List<Assembly>> DiscoveredProviders = new(DiscoverProviderAssemblies);

    private static List<Assembly> ProviderAssemblies => DiscoveredProviders.Value;

    // ── Facts ────────────────────────────────────────────────────────────────

    [Fact]
    public void Providers_are_discovered() =>
        // Guards the discovery mechanism itself: if the sender-interface heuristic breaks,
        // every other fact would silently pass on an empty set.
        ProviderAssemblies.Count.ShouldBeGreaterThanOrEqualTo(10);

    [Fact]
    public void Provider_packages_declare_options_with_SectionName() =>
        AssertCompliant(FailingOptionsDeclaration(), exemptions: EmptyExemptions, "declare an options class with 'public const string SectionName'");

    [Fact]
    public void Provider_modules_self_register_in_ConfigureServices() =>
        AssertCompliant(FailingSelfRegistration(), ProviderExemptions.SelfRegistrationPending, "override ConfigureServices in the module to self-register the provider");

    [Fact]
    public void Provider_packages_register_an_ActivitySource() =>
        AssertCompliant(FailingActivitySource(), ProviderExemptions.ActivitySourcePending, "ship an 'internal static *ActivitySource' with a Name const, registered via GranitActivitySourceRegistry");

    [Fact]
    public void Provider_packages_ship_a_health_check() =>
        AssertCompliant(FailingHealthCheck(), ProviderExemptions.HealthCheckPending, "ship an IHealthCheck implementation (non-mutating probe)");

    [Fact]
    public void Provider_packages_do_not_nest_under_a_channel() =>
        AssertCompliant(FailingPlacement(), ProviderExemptions.PlacementPending, "live at the top level (Granit.Notifications.{Provider}) as a capability implementer");

    [Fact]
    public void Provider_options_validation_is_effective() =>
        AssertCompliant(FailingValidation(), ProviderExemptions.ValidationPending, "use AddGranitProviderOptions (or ValidateDataAnnotations / IValidateOptions) so ValidateOnStart actually validates");

    [Fact]
    public void Provider_options_SectionName_is_asserted_in_tests() =>
        AssertCompliant(FailingSectionNameTest(), ProviderExemptions.SectionNameTestPending, "add an options test asserting SectionName.ShouldBe(\"…\")");

    [Fact]
    public void Provider_module_DependsOn_covers_direct_module_references() =>
        AssertCompliant(FailingDependsOn().Keys, ProviderExemptions.DependsOnPending, "declare every direct project reference that carries a *Module in [DependsOn]",
            details: FailingDependsOn());

    [Fact]
    public void Exemption_lists_contain_no_stale_entries()
    {
        List<string> stale = [];

        CollectStale(stale, nameof(ProviderExemptions.SelfRegistrationPending), ProviderExemptions.SelfRegistrationPending, FailingSelfRegistration());
        CollectStale(stale, nameof(ProviderExemptions.ActivitySourcePending), ProviderExemptions.ActivitySourcePending, FailingActivitySource());
        CollectStale(stale, nameof(ProviderExemptions.HealthCheckPending), ProviderExemptions.HealthCheckPending, FailingHealthCheck());
        CollectStale(stale, nameof(ProviderExemptions.PlacementPending), ProviderExemptions.PlacementPending, FailingPlacement());
        CollectStale(stale, nameof(ProviderExemptions.ValidationPending), ProviderExemptions.ValidationPending, FailingValidation());
        CollectStale(stale, nameof(ProviderExemptions.SectionNameTestPending), ProviderExemptions.SectionNameTestPending, FailingSectionNameTest());
        CollectStale(stale, nameof(ProviderExemptions.DependsOnPending), ProviderExemptions.DependsOnPending, FailingDependsOn().Keys);

        stale.ShouldBeEmpty(
            "These exemption entries are no longer failing (or no longer providers) — remove them from ProviderExemptions so the backlog stays honest.");
    }

    // ── Checks ───────────────────────────────────────────────────────────────

    private static List<string> FailingOptionsDeclaration() =>
        FailingProviders(assembly => GetLoadableTypes(assembly).Any(t =>
            t.Name.EndsWith("Options", StringComparison.Ordinal)
            && t.GetField("SectionName", BindingFlags.Public | BindingFlags.Static)?.IsLiteral == true));

    private static List<string> FailingSelfRegistration() =>
        FailingProviders(assembly =>
        {
            Type? module = FindModuleType(assembly);
            return module?.GetMethod("ConfigureServices", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null;
        });

    private static List<string> FailingActivitySource() =>
        FailingProviders(assembly => GetLoadableTypes(assembly).Any(t =>
            t.Name.EndsWith("ActivitySource", StringComparison.Ordinal)
            && t.IsAbstract && t.IsSealed // static class
            && t.GetField("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.IsLiteral == true));

    private static List<string> FailingHealthCheck() =>
        FailingProviders(assembly => GetLoadableTypes(assembly).Any(t =>
            !t.IsAbstract && t.GetInterfaces().Any(i =>
                i.FullName == "Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck")));

    private static List<string> FailingPlacement() =>
        FailingProviders(assembly =>
        {
            // Compliant: Granit.Notifications.{Provider} — exactly one segment after "Notifications".
            string name = assembly.GetName().Name!;
            return NestedProviderName().Count(name) == 0;
        });

    private static List<string> FailingValidation() =>
        FailingProviders(assembly =>
        {
            string srcDir = Path.Join(RepoRoot, "src", assembly.GetName().Name);
            if (!Directory.Exists(srcDir))
            {
                return true; // no source (external?) — nothing to check
            }

            bool usesHelper = false, usesDataAnnotations = false, hasValidator = false;
            foreach (string file in EnumerateSourceFiles(srcDir))
            {
                string content = File.ReadAllText(file);
                usesHelper |= content.Contains("AddGranitProviderOptions", StringComparison.Ordinal);
                usesDataAnnotations |= content.Contains("ValidateDataAnnotations()", StringComparison.Ordinal);
                hasValidator |= content.Contains("IValidateOptions<", StringComparison.Ordinal);
            }

            return usesHelper || usesDataAnnotations || hasValidator;
        });

    private static List<string> FailingSectionNameTest() =>
        FailingProviders(assembly =>
        {
            string testsDir = Path.Join(RepoRoot, "tests", assembly.GetName().Name! + ".Tests");
            return Directory.Exists(testsDir)
                && EnumerateSourceFiles(testsDir).Any(file =>
                    File.ReadLines(file).Any(line =>
                        line.Contains("SectionName", StringComparison.Ordinal)
                        && line.Contains("ShouldBe", StringComparison.Ordinal)));
        });

    private static Dictionary<string, string> FailingDependsOn()
    {
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Assembly assembly in ProviderAssemblies)
        {
            string name = assembly.GetName().Name!;
            Type? module = FindModuleType(assembly);
            if (module is null)
            {
                failures[name] = "no GranitModule subclass found";
                continue;
            }

            var declared = module
                .GetCustomAttributes<DependsOnAttribute>()
                .SelectMany(a => a.DependedTypes)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);

            List<string> missing = [];
            foreach (string referencedProject in ReadDirectProjectReferences(name))
            {
                if (referencedProject is "Granit")
                {
                    continue; // implicit base — never declared
                }

                Type? referencedModule = LoadedGranitAssemblies.Value
                    .FirstOrDefault(a => a.GetName().Name == referencedProject) is { } refAssembly
                        ? FindModuleType(refAssembly)
                        : null;

                if (referencedModule is not null && !declared.Contains(referencedModule.Name))
                {
                    missing.Add(referencedModule.Name);
                }
            }

            if (missing.Count > 0)
            {
                failures[name] = $"missing [DependsOn]: {string.Join(", ", missing)}";
            }
        }

        return failures;
    }

    // ── Discovery & plumbing ─────────────────────────────────────────────────

    private static readonly Lazy<List<Assembly>> LoadedGranitAssemblies = new(() =>
    {
        string outputDir = Path.GetDirectoryName(typeof(ProviderConventionTests).Assembly.Location)!;
        List<Assembly> loaded = [];
        foreach (string path in Directory.GetFiles(outputDir, "Granit.*.dll"))
        {
            try
            {
                loaded.Add(Assembly.LoadFrom(path));
            }
            catch (BadImageFormatException)
            {
                // Native/satellite artifacts — not managed Granit assemblies.
            }
        }

        return loaded;
    });

    private static List<Assembly> DiscoverProviderAssemblies()
    {
        var notificationAssemblies = LoadedGranitAssemblies.Value
            .Where(a => a.GetName().Name!.StartsWith("Granit.Notifications", StringComparison.Ordinal))
            .ToList();

        // Channel sender contracts: interfaces named I{X}Sender defined in the family.
        var senderInterfaces = notificationAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => t.IsInterface && SenderInterfaceName().IsMatch(t.Name))
            .ToHashSet();

        return notificationAssemblies
            .Where(a => GetLoadableTypes(a).Any(t =>
                t is { IsClass: true, IsAbstract: false }
                && t.GetInterfaces().Any(senderInterfaces.Contains)))
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> FailingProviders(Func<Assembly, bool> passes) =>
        ProviderAssemblies
            .Where(a => !passes(a))
            .Select(a => a.GetName().Name!)
            .ToList();

    private static readonly HashSet<string> EmptyExemptions = new(StringComparer.Ordinal);

    private static void AssertCompliant(
        IReadOnlyCollection<string> failing,
        HashSet<string> exemptions,
        string requirement,
        Dictionary<string, string>? details = null)
    {
        var violations = failing
            .Where(name => !exemptions.Contains(name))
            .Select(name => details is not null && details.TryGetValue(name, out string? d) ? $"{name} — {d}" : name)
            .ToList();

        violations.ShouldBeEmpty(
            $"Every notification provider package must {requirement}. "
            + "Fix the provider or add a justified entry to ProviderExemptions (with its removal issue).");
    }

    private static void CollectStale(
        List<string> stale, string listName, HashSet<string> exemptions, IReadOnlyCollection<string> currentlyFailing)
    {
        foreach (string entry in exemptions.Where(e => !currentlyFailing.Contains(e)))
        {
            stale.Add($"{listName}: {entry}");
        }
    }

    private static Type? FindModuleType(Assembly assembly) =>
        GetLoadableTypes(assembly).FirstOrDefault(t =>
            t is { IsClass: true, IsAbstract: false } && typeof(GranitModule).IsAssignableFrom(t));

    private static List<string> ReadDirectProjectReferences(string projectName)
    {
        string csproj = Path.Join(RepoRoot, "src", projectName, projectName + ".csproj");
        if (!File.Exists(csproj))
        {
            return [];
        }

        return XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

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

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ProviderConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")) || File.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    [GeneratedRegex(@"^I\w+Sender$")]
    private static partial Regex SenderInterfaceName();

    [GeneratedRegex(@"^Granit\.Notifications\.(Sms|Email|MobilePush|WebPush|WhatsApp)\..+$")]
    private static partial Regex NestedProviderName();
}
