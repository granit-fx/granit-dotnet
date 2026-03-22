using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates permission naming conventions in endpoint packages:
/// <list type="bullet">
/// <item>Permission constants must follow the <c>[Group].[Resource].[Action]</c> three-segment format</item>
/// <item>No <c>View</c> action — use <c>Read</c> instead (RBAC standard)</item>
/// </list>
/// </summary>
public sealed partial class PermissionConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// All permission string constants (except <c>GroupName</c>) in <c>*Permissions.cs</c>
    /// files must follow the <c>[Group].[Resource].[Action]</c> three-dot-separated format.
    /// </summary>
    [Fact]
    public void Permission_constants_should_use_three_segment_format()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetPermissionFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            int lineNumber = 0;

            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;
                Match match = PermissionConstant().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string name = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // GroupName is a single segment by design
                if (name == "GroupName")
                {
                    continue;
                }

                int segments = value.Split('.').Length;
                if (segments != 3)
                {
                    violations.Add(
                        $"{relativePath}:{lineNumber} {name} = \"{value}\" ({segments} segments, expected 3)");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Permission constants must use the [Group].[Resource].[Action] three-segment format. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Permission actions must not use <c>View</c> — use <c>Read</c> instead per RBAC standard.
    /// </summary>
    [Fact]
    public void Permission_actions_should_not_use_View()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetPermissionFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            int lineNumber = 0;

            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;
                Match match = PermissionConstant().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string name = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                if (name == "GroupName")
                {
                    continue;
                }

                if (value.EndsWith(".View", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{relativePath}:{lineNumber} {name} = \"{value}\" (use .Read, not .View)");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Permission actions must use 'Read', not 'View' (RBAC standard). " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static IEnumerable<string> GetPermissionFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*Permissions.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (!moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(PermissionConventionTests).Assembly.Location);
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
    /// Matches <c>public const string Name = "Value";</c> declarations.
    /// Group 1: constant name, Group 2: string value.
    /// </summary>
    [GeneratedRegex(@"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex PermissionConstant();
}
