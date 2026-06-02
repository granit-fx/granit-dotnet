using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable filesystem-based rules for Granit permission-naming conventions.
/// </summary>
public static partial class PermissionConventionRules
{
    /// <summary>
    /// All permission string constants (except <c>GroupName</c>) in <c>*Permissions.cs</c>
    /// files under <c>*.Endpoints</c> modules must follow the
    /// <c>[Group].[Resource].[Action]</c> three-segment format.
    /// </summary>
    public static void PermissionConstantsShouldUseThreeSegmentFormat(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetPermissionFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(srcDir, csFile);
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
    public static void PermissionActionsShouldNotUseView(string srcDir)
    {
        List<string> violations = [];

        foreach (string csFile in GetPermissionFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(srcDir, csFile);
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
    /// least one <c>RequireAuthorization(...)</c> call somewhere in the same module.
    /// </summary>
    public static void PermissionConstantsShouldBeReferencedByRequireAuthorization(string srcDir)
    {
        List<string> violations = [];

        foreach (string permissionFile in GetPermissionFiles(srcDir))
        {
            string moduleDir = ModuleDirectoryFor(permissionFile, srcDir);
            string[] moduleFiles = Directory.GetFiles(moduleDir, "*.cs", SearchOption.AllDirectories);
            string relativePermissionPath = Path.GetRelativePath(srcDir, permissionFile);

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

                    if (IsInBuildOutput(file))
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

    private static bool ContainsConstantReference(string content, string permissionValue)
    {
        string[] segments = permissionValue.Split('.');
        if (segments.Length != 3)
        {
            return false;
        }

        return content.Contains($".{segments[1]}.{segments[2]}", StringComparison.Ordinal);
    }

    private static IEnumerable<string> GetPermissionFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*Permissions.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                yield return csFile;
            }
        }
    }

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    [GeneratedRegex(@"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex PermissionConstant();
}
