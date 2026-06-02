using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable filesystem-based rules for background job conventions
/// (complementing the ArchUnitNET rules in <see cref="BackgroundJobConventionRules"/>).
/// </summary>
public static partial class BackgroundJobFilesystemRules
{
    /// <summary>
    /// <c>IBackgroundJob</c> implementors must reside in a <c>Jobs/</c> subfolder
    /// within their module, not at the module root or in other subfolders.
    /// </summary>
    public static void BackgroundJobsShouldResideInJobsFolder(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*Job.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

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
    /// <c>IBackgroundJob</c> types must not live in <c>*.Wolverine</c> packages — they belong
    /// in the module's <c>*.BackgroundJobs/Jobs/</c> sub-project.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="exemptedModules">Module names exempt from this rule (e.g. the infrastructure scheduling module).</param>
    public static void JobsShouldNotLiveInWolverinePackages(string srcDir, params string[] exemptedModules)
    {
        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*Job.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (exemptedModules.Contains(moduleName))
            {
                continue;
            }

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

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    [GeneratedRegex(@"(?:sealed\s+)?record\s+\w+\s*:\s*IBackgroundJob\b", RegexOptions.Multiline)]
    private static partial Regex ImplementsIBackgroundJob();
}
