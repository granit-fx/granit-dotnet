using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable filesystem-based rules for the Granit Isolated DbContext pattern.
/// </summary>
public static partial class IsolatedDbContextRules
{
    /// <summary>
    /// Module contexts must not override <c>OnModelCreating</c>: since the granit-dotnet
    /// #3159/#3162 overhaul, <c>GranitDbContext</c> owns the whole model pipeline (sealed
    /// <c>OnModelCreating</c>) and module contexts override <c>OnGranitModelCreating</c>.
    /// A raw override means the context escaped the base class — and it can no longer call
    /// the now-internal <c>ApplyGranitConventions</c>, silently losing every Granit convention.
    /// </summary>
    public static void ModuleContextsShouldNotOverrideOnModelCreating(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);
                if (OnModelCreatingOverride().IsMatch(content))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Module contexts must derive GranitDbContext and override OnGranitModelCreating — " +
            "a raw OnModelCreating override bypasses every Granit convention. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every concrete DbContext in <c>*.EntityFrameworkCore</c> packages must inherit
    /// <c>GranitDbContext</c> — the single conventions path since granit-dotnet #3162.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repository root for relative-path display.</param>
    /// <param name="exemptedFileNames">File names explicitly exempt (e.g. framework-internal contexts).</param>
    public static void DbContextClassesShouldDeriveFromGranitDbContext(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string>? exemptedFileNames = null)
    {
        exemptedFileNames ??= new HashSet<string>(["GranitDbContext.cs"]);
        List<string> violations = [];

        foreach (string project in Directory.GetDirectories(srcDir)
            .Where(d => Path.GetFileName(d).EndsWith(".EntityFrameworkCore", StringComparison.Ordinal)
                || Path.GetFileName(d).EndsWith(".Database", StringComparison.Ordinal)))
        {
            foreach (string csFile in Directory.GetFiles(project, "*DbContext.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(csFile);
                if (fileName.StartsWith('I') || exemptedFileNames.Contains(fileName))
                {
                    continue;
                }

                string content = File.ReadAllText(csFile);
                if (!content.Contains("sealed class", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!content.Contains(": GranitDbContext", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every concrete *DbContext.cs must inherit GranitDbContext — it owns the tenant " +
            "filter, the named convention filters, and the native convention engine. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// No manual <c>HasQueryFilter()</c> calls — <c>ApplyGranitConventions</c> handles all standard filters.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repository root for relative-path display.</param>
    /// <param name="exemptedRelativePaths">Relative path fragments to exempt (e.g. "OpenIddict").</param>
    public static void NoManualHasQueryFilterInEntityConfigurations(
        string srcDir,
        string repoRoot,
        params string[] exemptedRelativePaths)
    {
        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            foreach (string csFile in Directory.GetFiles(efProject, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(csFile);
                if (!content.Contains("HasQueryFilter", StringComparison.Ordinal))
                {
                    continue;
                }

                string rel = Path.GetRelativePath(repoRoot, csFile);

                if (exemptedRelativePaths.Any(ex => rel.Contains(ex, StringComparison.Ordinal)))
                {
                    continue;
                }

                violations.Add(rel);
            }
        }

        violations.ShouldBeEmpty(
            "Manual HasQueryFilter() is forbidden — ApplyGranitConventions handles all standard filters. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every <c>*.EntityFrameworkCore</c> project must reference <c>Granit.Persistence.EntityFrameworkCore</c>.
    /// </summary>
    public static void EfCoreProjectsShouldReferenceGranitPersistence(string srcDir)
    {
        List<string> violations = [];

        foreach (string efProject in GetEfCoreProjectDirs(srcDir))
        {
            string? csproj = Directory.GetFiles(efProject, "*.csproj").FirstOrDefault();
            if (csproj is null)
            {
                continue;
            }

            string content = File.ReadAllText(csproj);
            if (!content.Contains("Granit.Persistence.EntityFrameworkCore", StringComparison.Ordinal))
            {
                violations.Add(Path.GetFileName(efProject));
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore project must reference Granit.Persistence.EntityFrameworkCore " +
            "(isolated DbContext pattern). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every <c>*.EntityFrameworkCore</c> module class must declare
    /// <c>[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]</c>.
    /// </summary>
    public static void EfCoreModulesShouldDependOnGranitPersistenceModule(string srcDir, string repoRoot)
    {
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

                if (!content.Contains("GranitPersistenceEntityFrameworkCoreModule", StringComparison.Ordinal))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every *.EntityFrameworkCore module must have " +
            "[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static IEnumerable<string> GetEfCoreProjectDirs(string srcDir) =>
        Directory.GetDirectories(srcDir)
            .Where(d => Path.GetFileName(d).EndsWith(".EntityFrameworkCore", StringComparison.Ordinal));

    [GeneratedRegex(@"^\s+protected\s+override\s+void\s+OnModelCreating", RegexOptions.Multiline)]
    private static partial Regex OnModelCreatingOverride();
}
