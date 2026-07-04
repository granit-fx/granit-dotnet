using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Guards the architecture suite against silent coverage gaps. Every convention test discovers its
/// subjects by probing <c>Granit.*.dll</c> in <see cref="AppContext.BaseDirectory"/> (see
/// <c>ArchitectureLoader.Load</c>), so a module is only ever checked when <c>Granit.ArchitectureTests</c>
/// references it (directly or transitively) and its assembly lands in the output. A new
/// <c>*.Endpoints</c>/<c>*.EntityFrameworkCore</c> module whose <c>ProjectReference</c> is forgotten
/// would ship completely unchecked. This test asserts that every <c>src</c> project — bar the explicitly
/// justified exemptions — is present in the output directory.
/// </summary>
public sealed class ArchitectureTestCoverageTests
{
    /// <summary>
    /// Projects that legitimately produce no scannable domain code and are intentionally NOT referenced
    /// by the architecture suite. Every entry requires a justification.
    /// </summary>
    private static readonly HashSet<string> Exempt =
        new(StringComparer.Ordinal)
        {
            // Meta-packages: only DependsOn wiring, no domain/endpoint/EF types to scan.
            "Granit.Bundle.Api",
            "Granit.Bundle.Essentials",
            "Granit.Bundle.Notifications",
            "Granit.Bundle.OpenIddict",
            // Roslyn components (TargetFramework=netstandard2.0): cannot be referenced by a net10.0
            // test project, and ship no runtime domain types.
            "Granit.Analyzers",
            "Granit.Analyzers.CodeFixes",
            "Granit.Localization.SourceGenerator",
            // Build-time tool (OutputType=Exe), not a consumable module.
            "Granit.OpenApi.Generator",
        };

    [Fact]
    public void Every_src_module_assembly_is_loaded_by_the_architecture_suite()
    {
        string repoRoot = FindRepoRoot();
        string srcDir = Path.Combine(repoRoot, "src");

        var expected = Directory
            .EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !Exempt.Contains(name))
            .ToList();

        expected.ShouldNotBeEmpty("no src projects were discovered — check the repo-root probe.");

        var missing = expected
            .Where(name => !File.Exists(Path.Combine(AppContext.BaseDirectory, $"{name}.dll")))
            .Order(StringComparer.Ordinal)
            .ToList();

        missing.ShouldBeEmpty(
            "these src modules are absent from the architecture-test output and therefore escape every "
            + "convention rule. Add a <ProjectReference> in Granit.ArchitectureTests.csproj, or an exemption "
            + $"with justification: {string.Join(", ", missing)}");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Granit.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root (Granit.slnx not found).");
    }
}
