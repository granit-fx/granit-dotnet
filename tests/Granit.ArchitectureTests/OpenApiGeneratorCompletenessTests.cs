using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// The <c>Granit.OpenApi.Generator</c> build-only project must compose EVERY
/// <c>src/Granit.*.Endpoints</c> module so the published per-module OpenAPI contract artifacts
/// stay exhaustive. A new endpoints module that ships without being wired into the generator
/// silently drops its contract from the artifacts (and from the <c>granit-front</c> type feed) —
/// CI never notices because the generator still builds. This test fails the build instead.
/// </summary>
/// <remarks>
/// Source-text based (the generator is an <c>Exe</c> Web project we do not reference) and checks
/// three layers, so forgetting any one of them fails:
/// <list type="number">
///   <item>the <c>.csproj</c> references exactly the set of <c>src/Granit.*.Endpoints</c> projects;</item>
///   <item><c>GeneratorModule</c>'s <c>[DependsOn]</c> names each module (services are configured);</item>
///   <item><c>GeneratorEndpoints.All</c> has one registry entry per module (routes are mounted).</item>
/// </list>
/// </remarks>
public sealed partial class OpenApiGeneratorCompletenessTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string GeneratorDir = Path.Join(RepoRoot, "src", "Granit.OpenApi.Generator");

    private static IReadOnlyList<string> EndpointsProjectNames =>
        [.. Directory.EnumerateDirectories(Path.Join(RepoRoot, "src"), "Granit.*.Endpoints")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => File.Exists(Path.Join(RepoRoot, "src", name, $"{name}.csproj")))
            .OrderBy(name => name, StringComparer.Ordinal)];

    [Fact]
    public void Generator_csproj_references_exactly_the_endpoints_projects()
    {
        string csproj = File.ReadAllText(Path.Join(GeneratorDir, "Granit.OpenApi.Generator.csproj"));

        IReadOnlyList<string> referenced =
            [.. ProjectReferenceRegex().Matches(csproj)
                .Select(m => m.Groups["name"].Value)
                .Where(name => name.EndsWith(".Endpoints", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)];

        referenced.ShouldBe(EndpointsProjectNames,
            "Granit.OpenApi.Generator.csproj must <ProjectReference> exactly every src/Granit.*.Endpoints " +
            "project — no missing modules, no stale references.");
    }

    [Fact]
    public void Generator_module_DependsOn_names_every_endpoints_module()
    {
        string moduleSource = File.ReadAllText(Path.Join(GeneratorDir, "GeneratorModule.cs"));

        List<string> missing =
            [.. EndpointsProjectNames.Where(name => !moduleSource.Contains($"typeof({name}.", StringComparison.Ordinal))];

        missing.ShouldBeEmpty(
            "GeneratorModule's [DependsOn] must list each endpoints module so the Granit module " +
            "system configures its services. Missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void Generator_registry_maps_one_document_per_endpoints_module()
    {
        string registrySource = File.ReadAllText(Path.Join(GeneratorDir, "GeneratorEndpoints.cs"));

        int entries = RegistryEntryRegex().Count(registrySource);

        entries.ShouldBe(EndpointsProjectNames.Count,
            $"GeneratorEndpoints.All must declare one document per endpoints module " +
            $"({EndpointsProjectNames.Count} expected, found {entries}). Every module's routes must be mounted.");
    }

    [GeneratedRegex("""<ProjectReference\s+Include="[^"]*\\(?<name>[^"\\]+)\.csproj""")]
    private static partial Regex ProjectReferenceRegex();

    [GeneratedRegex("""new\("[a-z0-9-]+",""")]
    private static partial Regex RegistryEntryRegex();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(OpenApiGeneratorCompletenessTests).Assembly.Location);
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
}
