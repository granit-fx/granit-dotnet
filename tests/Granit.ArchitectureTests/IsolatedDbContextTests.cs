using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates the Isolated DbContext pattern checklist by scanning source files:
/// - OnModelCreating must call ApplyGranitConventions
/// - No manual HasQueryFilter (handled centrally by ApplyGranitConventions)
/// - *.EntityFrameworkCore.csproj must reference Granit.Persistence
/// </summary>
public sealed partial class IsolatedDbContextTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void OnModelCreating_should_call_ApplyGranitConventions()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);

                // Only check files with the actual DbContext override — skip interfaces, XML docs, extension methods.
                // Match "protected override void OnModelCreating" at the start of a line (not in comments).
                if (!OnModelCreatingOverride().IsMatch(content))
                {
                    continue;
                }

                if (!content.Contains("ApplyGranitConventions", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every DbContext.OnModelCreating must call modelBuilder.ApplyGranitConventions(). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void No_manual_HasQueryFilter_in_entity_configurations()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);

                if (content.Contains("HasQueryFilter", StringComparison.Ordinal))
                {
                    string rel = Path.GetRelativePath(RepoRoot, csFile);

                    // GranitUser cannot implement ISoftDeletable (incompatible with UserManager),
                    // so OpenIddict's model builder must register the soft-delete filter manually.
                    if (rel.Contains("OpenIddict", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(rel);
                }
            }
        }

        violations.ShouldBeEmpty(
            "Manual HasQueryFilter() is forbidden — ApplyGranitConventions handles all standard filters. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_projects_should_reference_GranitPersistence()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            string csproj = Directory.GetFiles(efProject, "*.csproj").FirstOrDefault()!;
            if (csproj is null)
            {
                continue;
            }

            string content = File.ReadAllText(csproj);

            if (!content.Contains("Granit.Persistence", StringComparison.Ordinal))
            {
                violations.Add(Path.GetFileName(efProject));
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore project must reference Granit.Persistence (isolated DbContext pattern). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_modules_should_DependOn_GranitPersistenceModule()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*Module.cs", SearchOption.TopDirectoryOnly))
            {
                string content = File.ReadAllText(csFile);

                if (!content.Contains(": GranitModule", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!content.Contains("GranitPersistenceModule", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore module must have [DependsOn(typeof(GranitPersistenceModule))]. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void EfCore_extension_methods_should_use_interceptor_DI_pattern()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            string extensionsDir = Path.Combine(efProject, "Extensions");
            if (!Directory.Exists(extensionsDir))
            {
                continue;
            }

            foreach (string csFile in Directory.GetFiles(extensionsDir, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);

                if (!content.Contains("AddDbContextFactory", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!content.Contains("ServiceLifetime.Scoped", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "AddDbContextFactory must use the (sp, options) overload with ServiceLifetime.Scoped " +
            "to resolve AuditedEntityInterceptor / SoftDeleteInterceptor. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static IEnumerable<string> GetEfCoreProjectDirs(string srcDir) =>
        Directory.GetDirectories(srcDir)
            .Where(d => Path.GetFileName(d).EndsWith(".EntityFrameworkCore", StringComparison.Ordinal));

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(IsolatedDbContextTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches the actual C# method override, not XML doc examples or comments.
    /// Requires whitespace before "protected" (indentation), excludes lines starting with "///".
    /// </summary>
    [GeneratedRegex(@"^\s+protected\s+override\s+void\s+OnModelCreating", RegexOptions.Multiline)]
    private static partial Regex OnModelCreatingOverride();
}
