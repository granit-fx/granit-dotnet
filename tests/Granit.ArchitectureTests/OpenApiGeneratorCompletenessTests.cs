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

    /// <summary>
    /// Slugs for HTTP surfaces that ship from a package whose name does NOT end in
    /// <c>.Endpoints</c>, so the <c>Granit.*.Endpoints</c> glob cannot discover them. Each must be
    /// wired into the generator explicitly (csproj <c>ProjectReference</c>, <c>GeneratorModule</c>
    /// <c>[DependsOn]</c>, and a <c>GeneratorEndpoints.All</c> entry) — they are counted here so the
    /// registry-completeness assertion stays exact. The query-engine exposes its query catalogue
    /// (<c>GET /catalog</c>) from <c>Granit.QueryEngine.Endpoints</c>.
    /// </summary>
    private static readonly IReadOnlyList<string> NonEndpointsDocumentSlugs = ["query-engine"];

    [Fact]
    public void Generator_csproj_references_exactly_the_endpoints_projects()
    {
        string csproj = File.ReadAllText(Path.Join(GeneratorDir, "Granit.OpenApi.Generator.csproj"));

        IReadOnlyList<string> referenced =
            [.. ProjectReferenceRegex().Matches(csproj)
                .Select(m => m.Groups["name"].Value)
                .Where(name => name.EndsWith(".Endpoints", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)];

        // A single wildcard ProjectReference (Granit.*.Endpoints) compile-references every
        // endpoints module by construction, so a new module is wired in automatically — there
        // is nothing to drift. Accept it as exhaustive. (DependsOn + registry completeness are
        // still enforced by the other two facts; the glob only auto-wires the compile reference.)
        if (referenced is ["Granit.*.Endpoints"])
        {
            return;
        }

        referenced.ShouldBe(EndpointsProjectNames,
            "Granit.OpenApi.Generator.csproj must <ProjectReference> exactly every src/Granit.*.Endpoints " +
            "project (or the single Granit.*.Endpoints wildcard) — no missing modules, no stale references.");
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
        int expected = EndpointsProjectNames.Count + NonEndpointsDocumentSlugs.Count;

        entries.ShouldBe(expected,
            "GeneratorEndpoints.All must declare one document per endpoints module plus the " +
            $"{NonEndpointsDocumentSlugs.Count} non-.Endpoints surface(s) [{string.Join(", ", NonEndpointsDocumentSlugs)}] " +
            $"({expected} expected, found {entries}). Every module's routes must be mounted.");

        // Each explicitly-wired non-.Endpoints surface must carry its declared slug.
        List<string> missing =
            [.. NonEndpointsDocumentSlugs.Where(slug => !registrySource.Contains($"new(\"{slug}\"", StringComparison.Ordinal))];

        missing.ShouldBeEmpty(
            "GeneratorEndpoints.All is missing a registry entry for non-.Endpoints surface(s): " + string.Join(", ", missing));
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
