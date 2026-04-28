using System.Reflection;
using System.Text.Json;
using Granit.Analytics.Metrics;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces D2 (#1397) of the BI epic (#1366): every <see cref="MetricDefinition{TEntity, TValue}"/>
/// MUST have a <c>Metric:{Name}</c> resource key in all 15 base cultures of its owning
/// module's <c>Localization/</c> directory. Regional variants (<c>fr-CA</c>, <c>en-GB</c>,
/// <c>pt-BR</c>) are exempt because they are designed to ship only differing keys and
/// fall through to their parent culture for everything else (see
/// <c>docs/guide/conventions/langues.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// The single failure aggregates all (metric, culture) pairs missing a key, so a
/// developer adding a new metric without a complete translation pack sees the entire
/// list at once instead of one-failure-per-culture spam.
/// </para>
/// <para>
/// A future <c>Dashboard:</c> companion check will be added when the first
/// <c>DashboardDefinition</c> ships under feature B (#1382).
/// </para>
/// </remarks>
public sealed class MetricLocalizationCompletenessTests
{
    private static readonly string[] BaseCultures =
    [
        "en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja",
        "pl", "tr", "ko", "sv", "cs", "hi",
    ];

    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_MetricDefinition_should_have_localization_keys_in_all_base_cultures()
    {
        IReadOnlyList<MetricDescriptor> metrics = ScanMetricDefinitions();
        metrics.ShouldNotBeEmpty(
            "No MetricDefinitions discovered — the test cannot run. " +
            "Ensure Granit.Analytics is referenced and reference modules (Granit.Invoicing) ship at least one metric.");

        List<string> missing = [];

        foreach (MetricDescriptor metric in metrics)
        {
            if (Path.IsPathRooted(metric.OwningAssemblyName))
            {
                missing.Add($"{metric.Name}: owning assembly name '{metric.OwningAssemblyName}' is rooted — refusing to combine into a path.");
                continue;
            }

            string moduleSrcDir = Path.Join(RepoRoot, "src", metric.OwningAssemblyName);
            string localizationDir = Path.Join(moduleSrcDir, "Localization");

            if (!Directory.Exists(localizationDir))
            {
                missing.Add($"{metric.Name} (declared in {metric.OwningAssemblyName}): Localization/ directory not found at {Path.GetRelativePath(RepoRoot, localizationDir)}");
                continue;
            }

            string resourceKey = $"Metric:{metric.Name}";

            foreach (string culture in BaseCultures)
            {
                bool found = LocateKeyInCultureFiles(localizationDir, culture, resourceKey);
                if (!found)
                {
                    missing.Add($"{metric.Name} -> {culture} (expected key '{resourceKey}' in src/{metric.OwningAssemblyName}/Localization/**/{culture}.json)");
                }
            }
        }

        missing.ShouldBeEmpty(
            $"D2 (#1397): every MetricDefinition must have a 'Metric:{{Name}}' key in all 15 base cultures " +
            $"of its owning module. {missing.Count} key(s) missing:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Order(StringComparer.Ordinal)));
    }

    private static bool LocateKeyInCultureFiles(string localizationDir, string culture, string resourceKey)
    {
        string[] candidateFiles = Directory.GetFiles(localizationDir, $"{culture}.json", SearchOption.AllDirectories);
        return candidateFiles.Any(file => FileContainsKey(file, resourceKey));
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

        // Granit localization files use either a flat root object ({ "Key": "Value" })
        // or a nested layout with the culture envelope ({ "culture": "...", "texts": { "Key": "Value" } }).
        if (root.TryGetProperty("texts", out JsonElement texts)
            && texts.ValueKind == JsonValueKind.Object
            && texts.TryGetProperty(key, out _))
        {
            return true;
        }

        return root.TryGetProperty(key, out _);
    }

    private static List<MetricDescriptor> ScanMetricDefinitions()
    {
        string outputDir = Path.GetDirectoryName(typeof(MetricLocalizationCompletenessTests).Assembly.Location)!;

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

        List<MetricDescriptor> metrics = [];

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

                if (!InheritsFromMetricDefinition(type))
                {
                    continue;
                }

                object instance;
                try { instance = Activator.CreateInstance(type)!; }
                catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException)
                {
                    // Definitions without a parameterless ctor are out of scope for this
                    // archi check — every shipped metric should be parameterless per the
                    // *.Analytics canonical checklist.
                    continue;
                }

                string metricName = (string)type.GetProperty(nameof(IMetricDefinitionDescriptor.Name))!.GetValue(instance)!;
                metrics.Add(new MetricDescriptor(metricName, assembly.GetName().Name!));
            }
        }

        return metrics;
    }

    private static bool InheritsFromMetricDefinition(Type candidate)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == typeof(MetricDefinition<,>))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(MetricLocalizationCompletenessTests).Assembly.Location);
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

    private sealed record MetricDescriptor(string Name, string OwningAssemblyName);
}
