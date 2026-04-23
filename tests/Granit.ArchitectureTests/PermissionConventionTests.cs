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
        string srcDir = Path.Join(RepoRoot, "src");

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
        string srcDir = Path.Join(RepoRoot, "src");

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

    /// <summary>
    /// Every permission constant declared in <c>*Permissions.cs</c> must be referenced by at
    /// least one <c>RequireAuthorization(...)</c> call in the same module. Catches dead
    /// constants and, more importantly, constants that are documented but never wired —
    /// e.g. a <c>Manage</c> permission left orphaned while writes inherit a <c>Read</c>
    /// group's policy.
    /// </summary>
    [Fact]
    public void Permission_constants_should_be_referenced_by_RequireAuthorization()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string permissionFile in GetPermissionFiles(srcDir))
        {
            string moduleDir = ModuleDirectoryFor(permissionFile, srcDir);
            string[] moduleFiles = Directory.GetFiles(moduleDir, "*.cs", SearchOption.AllDirectories);
            string relativePermissionPath = Path.GetRelativePath(RepoRoot, permissionFile);

            foreach ((string name, string value, int lineNumber) in EnumeratePermissionConstants(permissionFile))
            {
                if (name == "GroupName")
                {
                    continue;
                }

                bool referenced = false;
                foreach (string file in moduleFiles)
                {
                    if (string.Equals(file, permissionFile, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                        || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    {
                        continue;
                    }

                    string content = File.ReadAllText(file);
                    if (content.Contains($"\"{value}\"", StringComparison.Ordinal)
                        || ContainsConstantReference(content, value))
                    {
                        referenced = true;
                        break;
                    }
                }

                if (!referenced)
                {
                    violations.Add($"{relativePermissionPath}:{lineNumber} {name} = \"{value}\" is never referenced");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every permission constant must be wired to at least one RequireAuthorization call. " +
            $"Orphaned constants: {string.Join("; ", violations)}");
    }

    private static string ModuleDirectoryFor(string permissionFile, string srcDir)
    {
        string relativePath = Path.GetRelativePath(srcDir, permissionFile);
        string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];
        return Path.Join(srcDir, moduleName);
    }

    private static IEnumerable<(string Name, string Value, int LineNumber)> EnumeratePermissionConstants(string file)
    {
        int lineNumber = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNumber++;
            Match match = PermissionConstant().Match(line);
            if (match.Success)
            {
                yield return (match.Groups[1].Value, match.Groups[2].Value, lineNumber);
            }
        }
    }

    /// <summary>
    /// Detects qualified references such as <c>IdentityPermissions.Sessions.Manage</c>
    /// derived from the constant value <c>Identity.Sessions.Manage</c>.
    /// </summary>
    private static bool ContainsConstantReference(string content, string permissionValue)
    {
        string[] segments = permissionValue.Split('.');
        if (segments.Length != 3)
        {
            return false;
        }

        string qualified = $".{segments[1]}.{segments[2]}";
        return content.Contains(qualified, StringComparison.Ordinal);
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
    /// Matches <c>public const string Name = "Value";</c> declarations.
    /// Group 1: constant name, Group 2: string value.
    /// </summary>
    [GeneratedRegex(@"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex PermissionConstant();
}
