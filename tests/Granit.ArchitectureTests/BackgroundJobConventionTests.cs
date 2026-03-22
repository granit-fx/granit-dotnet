using System.Text.RegularExpressions;
using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates background job conventions:
/// <list type="bullet">
/// <item><c>*Job</c> suffix on <c>IBackgroundJob</c> implementors</item>
/// <item><c>[RecurringJob]</c> requires <c>IBackgroundJob</c></item>
/// <item><c>IBackgroundJob</c> types must reside in a <c>Jobs/</c> folder</item>
/// <item>Jobs must not live in a separate <c>*.Wolverine</c> package</item>
/// </list>
/// </summary>
public sealed partial class BackgroundJobConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Background_jobs_must_end_with_Job() =>
        BackgroundJobConventionRules.BackgroundJobsMustEndWithJob(Architecture, "Granit.");

    [Fact]
    public void Recurring_jobs_must_implement_IBackgroundJob() =>
        BackgroundJobConventionRules.RecurringJobsMustImplementIBackgroundJob(Architecture, "Granit.");

    /// <summary>
    /// <c>IBackgroundJob</c> implementors must reside in a <c>Jobs/</c> folder
    /// within their module, not at the module root or in other subfolders.
    /// </summary>
    [Fact]
    public void Background_jobs_should_reside_in_Jobs_folder()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*Job.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Only check files that implement IBackgroundJob
            string content = File.ReadAllText(csFile);
            if (!ImplementsIBackgroundJob().IsMatch(content))
            {
                continue;
            }

            string sep = Path.DirectorySeparatorChar.ToString();
            if (!csFile.Contains(sep + "Jobs" + sep, StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(srcDir, csFile));
            }
        }

        violations.ShouldBeEmpty(
            "IBackgroundJob implementors must reside in a Jobs/ subfolder within their module. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Jobs must not live in a separate <c>*.Wolverine</c> package — they belong in
    /// the base module's <c>Jobs/</c> folder. Wolverine scheduling is handled by
    /// <c>Granit.BackgroundJobs.Wolverine</c>.
    /// </summary>
    [Fact]
    public void Jobs_should_not_live_in_Wolverine_packages()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*Job.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            // Granit.BackgroundJobs.Wolverine is the scheduling infrastructure — exempt
            if (moduleName == "Granit.BackgroundJobs.Wolverine")
            {
                continue;
            }

            // Other *.Wolverine packages should not host job definitions
            if (moduleName.EndsWith(".Wolverine", StringComparison.Ordinal))
            {
                string content = File.ReadAllText(csFile);
                if (ImplementsIBackgroundJob().IsMatch(content))
                {
                    violations.Add(relativePath);
                }
            }
        }

        violations.ShouldBeEmpty(
            "IBackgroundJob types must not live in *.Wolverine packages — move them to the " +
            "base module's Jobs/ folder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(BackgroundJobConventionTests).Assembly.Location);
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
    /// Matches concrete type declarations (record/class) implementing <c>IBackgroundJob</c>.
    /// Excludes interface definitions and XML doc comments.
    /// </summary>
    [GeneratedRegex(@"(?:sealed\s+)?record\s+\w+\s*:\s*IBackgroundJob\b", RegexOptions.Multiline)]
    private static partial Regex ImplementsIBackgroundJob();
}
