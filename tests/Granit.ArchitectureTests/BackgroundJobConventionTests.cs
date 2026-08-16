using System.Text.RegularExpressions;
using Granit.ArchitectureTests.Abstractions;
using Granit.ArchitectureTests.Abstractions.Rules;
using Granit.BackgroundJobs;
using Granit.BackgroundJobs.Internal;
using Granit.Reflection;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates background job conventions:
/// <list type="bullet">
/// <item><c>*Job</c> suffix on <c>IBackgroundJob</c> implementors</item>
/// <item><c>[RecurringJob]</c> requires <c>IBackgroundJob</c></item>
/// <item><c>IBackgroundJob</c> types must reside in a <c>Jobs/</c> folder</item>
/// <item>Jobs must not live in a <c>*.Wolverine</c> package</item>
/// <item>every <c>IBackgroundJob</c> resolves a handler on the channel path</item>
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
        string srcDir = Path.Join(RepoRoot, "src");

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
    /// Jobs must not live in a <c>*.Wolverine</c> package — they belong in
    /// the module's <c>*.BackgroundJobs/Jobs/</c> sub-project. Wolverine scheduling
    /// is handled by <c>Granit.BackgroundJobs.Wolverine</c>.
    /// </summary>
    [Fact]
    public void Jobs_should_not_live_in_Wolverine_packages()
    {
        string srcDir = Path.Join(RepoRoot, "src");

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
            "IBackgroundJob types must not live in *.Wolverine packages — move them to a " +
            "*.BackgroundJobs sub-project's Jobs/ folder. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every <c>IBackgroundJob</c> must bind a handler under the in-process channel worker's
    /// rule, not only under Wolverine's. The two dispatch paths are advertised as equivalent
    /// and the channel path is the documented default — a job that only resolves under
    /// Wolverine fails silently forever on that default (#3206).
    /// </summary>
    [Fact]
    public void Jobs_must_resolve_a_handler_on_the_channel_path()
    {
        List<Type> jobs =
        [
            .. ArchitectureLoader.LoadAssemblies("Granit.", typeof(BackgroundJobConventionTests).Assembly)
                .SelectMany(static a => a.GetLoadableTypes())
                .Where(static t => typeof(IBackgroundJob).IsAssignableFrom(t)
                    && t is { IsInterface: false, IsAbstract: false, IsGenericTypeDefinition: false })
                .OrderBy(static t => t.FullName, StringComparer.Ordinal),
        ];

        // Guard against a green run caused by an empty scan (missing ProjectReference).
        jobs.ShouldNotBeEmpty("No IBackgroundJob types found — the satellite packages are " +
            "probably not referenced by Granit.ArchitectureTests.csproj.");

        List<string> violations = [];

        foreach (Type job in jobs)
        {
            try
            {
                BackgroundJobHandlerResolver.Resolve(job);
            }
            catch (InvalidOperationException ex)
            {
                violations.Add($"{job.FullName}: {ex.Message}");
            }
        }

        violations.ShouldBeEmpty(
            "Every IBackgroundJob must have exactly one handler in its own assembly: a public " +
            "non-generic type whose name ends in 'Handler'/'Consumer', with a public HandleAsync/Handle " +
            "method taking the job as first parameter. " +
            $"Violators:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(BackgroundJobConventionTests).Assembly.Location);
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

    /// <summary>
    /// Matches concrete type declarations (record/class) implementing <c>IBackgroundJob</c>.
    /// Excludes interface definitions and XML doc comments.
    /// </summary>
    [GeneratedRegex(@"(?:sealed\s+)?record\s+\w+\s*:\s*IBackgroundJob\b", RegexOptions.Multiline)]
    private static partial Regex ImplementsIBackgroundJob();
}
