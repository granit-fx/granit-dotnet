using System.Reflection;
using System.Text.Json;
using Granit.Dashboards;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Companion to <see cref="MetricLocalizationCompletenessTests"/> — completes the D2
/// (#1397) coverage by extending the same archi rule to <see cref="DashboardDefinition"/>
/// instances. Every concrete dashboard MUST have a <c>Dashboard:{Name}</c> resource
/// key in all 15 base cultures of its owning module's <c>Localization/</c> directory.
/// Regional variants (<c>fr-CA</c>, <c>en-GB</c>, <c>pt-BR</c>) are exempt by design —
/// they ship only differing keys and fall through to their parent culture.
/// </summary>
/// <remarks>
/// <para>
/// Stays trivially green today (no framework module ships a <c>DashboardDefinition</c>
/// yet). Becomes load-bearing the moment <c>Granit.Invoicing</c> or any other module
/// ships its first dashboard — which is the whole point of pinning the rule before
/// the first instance ships, not after.
/// </para>
/// <para>
/// Failure aggregates every missing (dashboard, culture) pair into a single message
/// — same UX as the metric companion.
/// </para>
/// </remarks>
public sealed class DashboardLocalizationCompletenessTests
{
    private static readonly string[] BaseCultures =
    [
        "en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja",
        "pl", "tr", "ko", "sv", "cs", "hi",
    ];

    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_DashboardDefinition_should_have_localization_keys_in_all_base_cultures()
    {
        IReadOnlyList<DashboardDescriptor> dashboards = ScanDashboardDefinitions();

        if (dashboards.Count == 0)
        {
            // Pinned-empty: no DashboardDefinition has shipped yet (B6+ stories
            // bring the first ones). The rule is in place ahead of time so the
            // first dashboard PR fails CI loudly if it forgets the keys.
            return;
        }

        List<string> missing = [];

        foreach (DashboardDescriptor dashboard in dashboards)
        {
            string moduleSrcDir = Path.Combine(RepoRoot, "src", dashboard.OwningAssemblyName);
            string localizationDir = Path.Combine(moduleSrcDir, "Localization");

            if (!Directory.Exists(localizationDir))
            {
                missing.Add($"{dashboard.Name} (declared in {dashboard.OwningAssemblyName}): Localization/ directory not found at {Path.GetRelativePath(RepoRoot, localizationDir)}");
                continue;
            }

            string resourceKey = $"Dashboard:{dashboard.Name}";

            foreach (string culture in BaseCultures)
            {
                if (!LocateKeyInCultureFiles(localizationDir, culture, resourceKey))
                {
                    missing.Add($"{dashboard.Name} -> {culture} (expected key '{resourceKey}' in src/{dashboard.OwningAssemblyName}/Localization/**/{culture}.json)");
                }
            }
        }

        missing.ShouldBeEmpty(
            $"D2 (#1397): every DashboardDefinition must have a 'Dashboard:{{Name}}' key in all 15 base cultures " +
            $"of its owning module. {missing.Count} key(s) missing:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    private static bool LocateKeyInCultureFiles(string localizationDir, string culture, string resourceKey)
    {
        string[] candidateFiles = Directory.GetFiles(localizationDir, $"{culture}.json", SearchOption.AllDirectories);

        foreach (string file in candidateFiles)
        {
            if (FileContainsKey(file, resourceKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FileContainsKey(string filePath, string key)
    {
        using FileStream stream = File.OpenRead(filePath);
        using var document = JsonDocument.Parse(stream);

        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("texts", out JsonElement texts)
            && texts.ValueKind == JsonValueKind.Object
            && texts.TryGetProperty(key, out _))
        {
            return true;
        }

        return root.TryGetProperty(key, out _);
    }

    private static List<DashboardDescriptor> ScanDashboardDefinitions()
    {
        string outputDir = Path.GetDirectoryName(typeof(DashboardLocalizationCompletenessTests).Assembly.Location)!;

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

        List<DashboardDescriptor> dashboards = [];

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

                if (!typeof(DashboardDefinition).IsAssignableFrom(type))
                {
                    continue;
                }

                DashboardDefinition? instance;
                try { instance = Activator.CreateInstance(type) as DashboardDefinition; }
                catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException)
                {
                    // Definitions without a parameterless ctor are out of scope for this
                    // archi check — every shipped dashboard should be parameterless per
                    // the *.Dashboards canonical placement rules.
                    continue;
                }

                if (instance is null)
                {
                    continue;
                }

                dashboards.Add(new DashboardDescriptor(instance.Name, assembly.GetName().Name!));
            }
        }

        return dashboards;
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(DashboardLocalizationCompletenessTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    private sealed record DashboardDescriptor(string Name, string OwningAssemblyName);
}
