using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the framework/module boundary:
/// - Framework packages must never reference module packages.
/// - Every src/ package must be classifiable as framework, module, or bundle.
/// See docs/concepts/framework-vs-modules for the full classification.
/// </summary>
public sealed class FrameworkBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Framework packages (horizontal infrastructure) must not have a ProjectReference
    /// to any module package (vertical business feature). This ensures dependency flows
    /// from modules to framework, never the reverse.
    /// </summary>
    [Fact]
    public void Framework_packages_should_not_reference_module_packages() =>
        FrameworkBoundaryRules.FrameworkProjectsShouldNotDependOnModules(RepoRoot);

    /// <summary>
    /// Every <c>.csproj</c> under <c>src/</c> must be classifiable as framework, module,
    /// or bundle. Unclassified projects indicate a new package was added without updating
    /// the module root registry in <see cref="FrameworkBoundaryRules.ModuleRootPrefixes"/>.
    /// </summary>
    [Fact]
    public void Every_src_package_should_be_classified_as_framework_or_module()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> unclassified = [];

        foreach (string csproj in Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(csproj);
            string relativePath = Path.GetRelativePath(RepoRoot, csproj).Replace('\\', '/');
            string classification = FrameworkBoundaryRules.ClassifyProject(projectName, relativePath);

            if (string.Equals(classification, "unknown", StringComparison.Ordinal))
            {
                unclassified.Add(projectName);
            }
        }

        unclassified.ShouldBeEmpty(
            "Every src/ project must be classified as framework, module, or bundle. " +
            "If you added a new module, add its root prefix to FrameworkBoundaryRules.ModuleRootPrefixes. " +
            $"Unclassified: {string.Join(", ", unclassified)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(FrameworkBoundaryTests).Assembly.Location);
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
