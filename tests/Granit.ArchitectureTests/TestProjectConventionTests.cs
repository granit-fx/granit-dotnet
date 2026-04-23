using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates test project conventions: every src package must have a matching test project.
/// </summary>
public sealed class TestProjectConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Every_src_package_should_have_a_test_project()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        string testsDir = Path.Join(RepoRoot, "tests");

        // Auto-exclude analyzers and source generators (target netstandard2.0,
        // cannot reference net10.0 test infrastructure)
        IEnumerable<string> srcPackages = Directory.GetDirectories(srcDir)
            .Select(Path.GetFileName)
            .Where(name => name!.StartsWith("Granit.", StringComparison.Ordinal))
            .Where(name => File.Exists(Path.Join(srcDir, name!, $"{name}.csproj")))
            .Where(name => !TargetsNetStandard(Path.Join(srcDir, name!, $"{name}.csproj")))
            .Cast<string>();

        List<string> missing = [];
        foreach (string package in srcPackages)
        {
            bool hasUnitTests = Directory.Exists(Path.Join(testsDir, $"{package}.Tests"));
            bool hasIntegrationTests = Directory.Exists(Path.Join(testsDir, $"{package}.Tests.Integration"));

            if (!hasUnitTests && !hasIntegrationTests)
            {
                missing.Add(package);
            }
        }

        missing.ShouldBeEmpty(
            "Every src package must have a matching tests/ project (DoD). " +
            $"Missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_src_package_should_have_a_README()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        IEnumerable<string> srcPackages = Directory.GetDirectories(srcDir)
            .Select(Path.GetFileName)
            .Where(name => name!.StartsWith("Granit.", StringComparison.Ordinal))
            .Where(name => File.Exists(Path.Join(srcDir, name!, $"{name}.csproj")))
            .Cast<string>();

        List<string> missing = [];
        foreach (string package in srcPackages)
        {
            string readmePath = Path.Join(srcDir, package, "README.md");
            if (!File.Exists(readmePath))
            {
                missing.Add(package);
            }
        }

        missing.ShouldBeEmpty(
            "Every src package must have a README.md. " +
            $"Missing: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Returns <c>true</c> when the csproj targets <c>netstandard*</c> — these are
    /// analyzers/source generators that cannot have standard test projects.
    /// </summary>
    private static bool TargetsNetStandard(string csprojPath)
    {
        string content = File.ReadAllText(csprojPath);
        return content.Contains("<TargetFramework>netstandard", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(TestProjectConventionTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git"); if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }
}
