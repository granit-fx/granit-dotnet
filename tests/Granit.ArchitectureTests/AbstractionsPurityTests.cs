using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that <c>*.Abstractions</c> packages stay lightweight: they may only
/// reference the foundational <c>Granit</c> modularity package and other
/// <c>*.Abstractions</c> packages — never a runtime sibling. This guarantees
/// that any base module pulling an <c>*.Abstractions</c> package does not
/// transitively drag in the runtime engine, EF Core, hosting infrastructure,
/// or DI container concretions.
/// </summary>
public sealed partial class AbstractionsPurityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("Granit.Workflow.Abstractions")]
    [InlineData("Granit.Dashboards.Abstractions")]
    [InlineData("Granit.Entities.Abstractions")]
    [InlineData("Granit.Entities.Views.Abstractions")]
    public void Abstractions_csproj_should_only_reference_other_abstractions_or_Granit_root(string packageName)
    {
        string csproj = Path.Join(RepoRoot, "src", packageName, $"{packageName}.csproj");
        File.Exists(csproj).ShouldBeTrue($"csproj not found at {csproj}");

        string content = File.ReadAllText(csproj);

        List<string> violations = [];

        foreach (Match match in ProjectReferenceInclude().Matches(content))
        {
            // csproj paths use Windows-style separators; normalize for cross-platform parsing.
            string refPath = match.Groups[1].Value.Replace('\\', '/');
            string refName = Path.GetFileNameWithoutExtension(refPath);

            bool allowed = refName == "Granit"
                || refName.EndsWith(".Abstractions", StringComparison.Ordinal);

            if (!allowed)
            {
                violations.Add(refName);
            }
        }

        violations.ShouldBeEmpty(
            $"{packageName} must only reference Granit (modularity) or other *.Abstractions packages. " +
            $"Disallowed references: {string.Join(", ", violations)}");
    }

    [Theory]
    [InlineData("Granit.Workflow.Abstractions")]
    [InlineData("Granit.Dashboards.Abstractions")]
    [InlineData("Granit.Entities.Abstractions")]
    [InlineData("Granit.Entities.Views.Abstractions")]
    public void Abstractions_csproj_should_not_reference_aspnetcore_efcore_or_hosting(string packageName)
    {
        string csproj = Path.Join(RepoRoot, "src", packageName, $"{packageName}.csproj");
        File.Exists(csproj).ShouldBeTrue($"csproj not found at {csproj}");

        string content = File.ReadAllText(csproj);

        List<string> violations = [];

        foreach (Match match in PackageReferenceInclude().Matches(content))
        {
            string packageRef = match.Groups[1].Value;

            if (packageRef.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || packageRef.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || packageRef.StartsWith("Microsoft.Extensions.Hosting", StringComparison.Ordinal)
                || packageRef.StartsWith("FluentValidation", StringComparison.Ordinal))
            {
                violations.Add(packageRef);
            }
        }

        // FrameworkReference Include="Microsoft.AspNetCore.App" is also forbidden
        if (FrameworkReferenceAspNetCore().IsMatch(content))
        {
            violations.Add("FrameworkReference Microsoft.AspNetCore.App");
        }

        violations.ShouldBeEmpty(
            $"{packageName} must remain runtime-agnostic. " +
            $"Disallowed package/framework references: {string.Join(", ", violations)}");
    }

    [GeneratedRegex(@"<ProjectReference\s+Include\s*=\s*""([^""]+)""\s*/?>")]
    private static partial Regex ProjectReferenceInclude();

    [GeneratedRegex(@"<PackageReference\s+Include\s*=\s*""([^""]+)""")]
    private static partial Regex PackageReferenceInclude();

    [GeneratedRegex(@"<FrameworkReference\s+Include\s*=\s*""Microsoft\.AspNetCore\.App""")]
    private static partial Regex FrameworkReferenceAspNetCore();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(AbstractionsPurityTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            // .git is a directory in the main checkout, but a file in a git worktree
            // (the file points to the gitdir of the parent). Accept either to support both.
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not locate repository root (no .git found).");
    }
}
