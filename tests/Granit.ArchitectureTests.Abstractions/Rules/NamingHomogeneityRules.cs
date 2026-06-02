using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rules for module naming homogeneity: namespace matches project directory,
/// type name matches file name. Catches rename residues.
/// </summary>
public static partial class NamingHomogeneityRules
{
    /// <summary>
    /// Every <c>.cs</c> file whose relative path starts with <paramref name="projectPrefix"/>
    /// must declare a namespace that starts with its containing project directory name.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="projectPrefix">Directory-name prefix of module projects (e.g. <c>"Granit."</c>).</param>
    public static void NamespaceShouldMatchProjectDirectoryName(string srcDir, string projectPrefix = "Granit.")
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir, projectPrefix))
        {
            string projectName = GetProjectDirectoryName(srcDir, csFile);
            string? ns = ExtractNamespace(csFile);

            if (ns is null)
            {
                continue;
            }

            string effectiveProjectName = projectName.EndsWith(".Abstractions", StringComparison.Ordinal)
                ? projectName[..^".Abstractions".Length]
                : projectName;

            if (!ns.StartsWith(effectiveProjectName, StringComparison.Ordinal))
            {
                string rel = Path.GetRelativePath(srcDir, csFile);
                violations.Add($"{rel}: namespace '{ns}' does not start with '{projectName}'");
            }
        }

        violations.ShouldBeEmpty(
            "Namespace declarations must match the containing project directory name. " +
            "This is often caused by a module rename where the namespace was not updated. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// For single-type files, at least one declared type name must match the file name.
    /// Multi-type files (intentional groupings) are only flagged when they contain
    /// exactly one type that does not match.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="projectPrefix">Directory-name prefix of module projects (e.g. <c>"Granit."</c>).</param>
    public static void FileShouldContainTypeMatchingItsName(string srcDir, string projectPrefix = "Granit.")
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir, projectPrefix))
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);

            if (fileName is "GlobalUsings" or "AssemblyInfo" or "AssemblyAttributes")
            {
                continue;
            }

            List<string> typeNames = ExtractAllTypeNames(csFile);
            if (typeNames.Count == 0)
            {
                continue;
            }

            bool hasMatch = typeNames.Any(t => TypeNameMatchesFileName(t, fileName));
            if (!hasMatch)
            {
                if (typeNames.Count > 1)
                {
                    continue;
                }

                string rel = Path.GetRelativePath(srcDir, csFile);
                violations.Add($"{rel}: type '{typeNames[0]}' does not match file name '{fileName}'");
            }
        }

        violations.ShouldBeEmpty(
            "Each file must contain at least one type whose name matches the file name. " +
            "This is often caused by a module rename where the class was not updated. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static string? ExtractNamespace(string filePath)
    {
        foreach (string line in File.ReadLines(filePath))
        {
            Match match = NamespaceDeclaration().Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return null;
    }

    private static List<string> ExtractAllTypeNames(string filePath)
    {
        List<string> types = [];
        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal)
                || trimmed.StartsWith('*'))
            {
                continue;
            }

            Match match = TypeDeclaration().Match(line);
            if (match.Success)
            {
                types.Add(match.Groups[1].Value);
            }
        }
        return types;
    }

    private static bool TypeNameMatchesFileName(string typeName, string fileName)
    {
        string name = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;
        if (string.Equals(name, fileName, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals("I" + fileName, name, StringComparison.Ordinal))
        {
            return true;
        }

        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])
            && string.Equals(name[1..], fileName, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetSrcCsFiles(string srcDir, string projectPrefix) =>
        Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                string rel = Path.GetRelativePath(srcDir, f);
                return rel.StartsWith(projectPrefix, StringComparison.Ordinal)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar);
            });

    private static string GetProjectDirectoryName(string srcDir, string filePath)
    {
        string relativePath = Path.GetRelativePath(srcDir, filePath);
        int sepIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
        return sepIndex >= 0 ? relativePath[..sepIndex] : relativePath;
    }

    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();

    [GeneratedRegex(
        @"^(?:public|internal|file)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*" +
        @"(?:class|record(?:\s+struct)?|struct|interface|enum)\s+(\w+(?:<[\w,\s]+>)?)",
        RegexOptions.Multiline)]
    private static partial Regex TypeDeclaration();
}
