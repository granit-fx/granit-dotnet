using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates naming homogeneity across module projects:
/// <list type="bullet">
/// <item>Namespace declarations must match the containing project name</item>
/// <item>Primary type name in a file must match the file name</item>
/// </list>
/// Catches residues left behind after module renames (e.g., old namespace or class
/// name surviving inside a renamed project directory).
/// </summary>
public sealed partial class NamingHomogeneityTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    /// <summary>
    /// Every <c>.cs</c> file in <c>src/Granit.{Module}*/</c> must declare a namespace
    /// that starts with its containing project directory name.
    /// <para>
    /// Catches: file moved to a renamed project directory without updating the
    /// <c>namespace</c> declaration. For example, <c>namespace Granit.QueryEngine;</c>
    /// inside <c>src/Granit.QueryEngine/</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void Namespace_should_match_project_directory_name()
    {
        List<string> violations = [];

        foreach (string csFile in GetGranitSrcCsFiles())
        {
            string projectName = GetProjectDirectoryName(csFile);
            string? ns = ExtractNamespace(csFile);

            if (ns is null)
            {
                continue;
            }

            // Namespace must start with the project directory name.
            // Sub-namespaces (Granit.BlobStorage.Internal) are fine as long
            // as they start with the project name (Granit.BlobStorage).
            // Exception: *.Abstractions packages use the parent namespace
            // (e.g. Granit.QueryEngine.Abstractions → namespace Granit.QueryEngine)
            // following the Microsoft.Extensions.*.Abstractions convention.
            string effectiveProjectName = projectName.EndsWith(".Abstractions", StringComparison.Ordinal)
                ? projectName[..^".Abstractions".Length]
                : projectName;

            if (!ns.StartsWith(effectiveProjectName, StringComparison.Ordinal))
            {
                string rel = Path.GetRelativePath(SrcRoot, csFile);
                violations.Add($"{rel}: namespace '{ns}' does not start with '{projectName}'");
            }
        }

        violations.ShouldBeEmpty(
            "Namespace declarations must match the containing project directory name. " +
            "This is often caused by a module rename where the namespace was not updated. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// At least one type declared in a <c>.cs</c> file must match the file name
    /// (without extension). Catches renamed files where the type declaration
    /// was not updated.
    /// <para>
    /// Files may contain multiple types (e.g., an enum before the main class).
    /// The test passes as long as any declared type matches the file name.
    /// </para>
    /// <para>
    /// Excludes: <c>GlobalUsings.cs</c>, generated files, and files containing
    /// only extension methods (where the static class name may legitimately differ).
    /// </para>
    /// </summary>
    [Fact]
    public void File_should_contain_a_type_matching_its_name()
    {
        List<string> violations = [];

        foreach (string csFile in GetGranitSrcCsFiles())
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);

            // Skip well-known files that legitimately don't follow the convention
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
                // Multi-type files with no match are often intentional groupings
                // (e.g., PasskeyRequests.cs with PasskeyRegistrationRequest + PasskeyRenameRequest).
                // Only flag single-type files — those are the clear rename residues.
                if (typeNames.Count > 1)
                {
                    continue;
                }

                string rel = Path.GetRelativePath(SrcRoot, csFile);
                violations.Add($"{rel}: type '{typeNames[0]}' does not match file name '{fileName}'");
            }
        }

        violations.ShouldBeEmpty(
            "Each file must contain at least one type whose name matches the file name. " +
            "This is often caused by a module rename where the class was not updated. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Extracts the file-scoped or first block-scoped namespace from a C# file.
    /// Returns <c>null</c> if no namespace declaration is found.
    /// </summary>
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

    /// <summary>
    /// Extracts all top-level type names from a C# file.
    /// Returns an empty list if no type declarations are found.
    /// </summary>
    private static List<string> ExtractAllTypeNames(string filePath)
    {
        List<string> types = [];

        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.TrimStart();

            // Skip comments
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

    /// <summary>
    /// Checks if a declared type name matches the expected file name, accounting for:
    /// <list type="bullet">
    /// <item>Generic type parameters (<c>TypeName&lt;T&gt;</c> → <c>TypeName</c>)</item>
    /// <item>Interface prefix (<c>IMyService</c> matches <c>MyService.cs</c> and vice versa)</item>
    /// </list>
    /// </summary>
    private static bool TypeNameMatchesFileName(string typeName, string fileName)
    {
        // Strip generic parameters: TypeName<T> → TypeName
        string name = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;

        // Exact match: MyClass.cs → class MyClass
        if (string.Equals(name, fileName, StringComparison.Ordinal))
        {
            return true;
        }

        // Interface in file named after impl: FeatureDefinitionProvider.cs → IFeatureDefinitionProvider
        if (string.Equals("I" + fileName, name, StringComparison.Ordinal))
        {
            return true;
        }

        // Impl in file named after interface: IMyService.cs → MyService
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])
            && string.Equals(name[1..], fileName, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetGranitSrcCsFiles() =>
        Directory.EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                string rel = Path.GetRelativePath(SrcRoot, f);
                return rel.StartsWith("Granit.", StringComparison.Ordinal)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar);
            });

    /// <summary>
    /// Gets the project directory name (e.g., <c>Granit.QueryEngine.EntityFrameworkCore</c>)
    /// for a source file path.
    /// </summary>
    private static string GetProjectDirectoryName(string filePath)
    {
        string relativePath = Path.GetRelativePath(SrcRoot, filePath);
        int sepIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
        return sepIndex >= 0 ? relativePath[..sepIndex] : relativePath;
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(NamingHomogeneityTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Combine(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches file-scoped (<c>namespace X;</c>) or block-scoped (<c>namespace X {</c>)
    /// namespace declarations. Group 1 captures the namespace identifier.
    /// </summary>
    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();

    /// <summary>
    /// Matches type declarations in a file (class, record, struct, interface, enum).
    /// Group 1 captures the type name (including generic parameters if present).
    /// Only matches top-level declarations (not nested types).
    /// <para>
    /// Note: <c>record</c> does NOT include a trailing <c>\s+</c> inside the alternation,
    /// because the outer <c>\s+</c> after the group handles the separator. The optional
    /// <c>struct</c> suffix for <c>record struct</c> is handled separately.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"^(?:public|internal|file)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*" +
        @"(?:class|record(?:\s+struct)?|struct|interface|enum)\s+(\w+(?:<[\w,\s]+>)?)",
        RegexOptions.Multiline)]
    private static partial Regex TypeDeclaration();
}
