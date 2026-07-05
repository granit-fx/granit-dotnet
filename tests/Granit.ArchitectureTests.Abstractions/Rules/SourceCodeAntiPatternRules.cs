using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable filesystem-based rules that detect common C# anti-patterns.
/// </summary>
public static partial class SourceCodeAntiPatternRules
{
    /// <summary>Asserts no <c>async void</c> methods exist under <paramref name="srcDir"/>.</summary>
    public static void AsyncVoidMethodsShouldNotExist(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            foreach (Match match in AsyncVoidMethod().Matches(content))
            {
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(repoRoot, csFile)}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "async void methods are forbidden — exceptions cannot be caught and will crash the process. " +
            "Use async Task instead. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>Asserts no <c>throw ex;</c> statements exist under <paramref name="srcDir"/>.</summary>
    public static void ThrowExShouldNotBeUsed(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            foreach (Match match in ThrowExStatement().Matches(content))
            {
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(repoRoot, csFile)}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "throw ex; destroys the original stack trace — use throw; to preserve it. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Asserts that namespaces match the folder structure: <c>{ProjectName}.{SubFolder1}.{SubFolder2}</c>.
    /// </summary>
    /// <param name="srcDir">Path to <c>src/</c>.</param>
    /// <param name="repoRoot">Repository root for relative-path display.</param>
    /// <param name="groupingFolderPrefixes">
    /// Optional organizational folder names (not part of the namespace) directly under <c>src/</c>.
    /// For example, in granit-dotnet, <c>"bundles"</c> lives under src but is not a namespace segment.
    /// </param>
    public static void NamespaceShouldMatchFolderStructure(
        string srcDir,
        string repoRoot,
        params string[] groupingFolderPrefixes)
    {
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            Match nsMatch = NamespaceDeclaration().Match(content);
            if (!nsMatch.Success)
            {
                continue;
            }

            string actualNamespace = nsMatch.Groups[1].Value;
            string expectedNamespace = ComputeExpectedNamespace(srcDir, csFile, groupingFolderPrefixes);

            if (!string.Equals(actualNamespace, expectedNamespace, StringComparison.Ordinal))
            {
                string relativePath = Path.GetRelativePath(repoRoot, csFile);
                violations.Add($"{relativePath} (expected: {expectedNamespace}, actual: {actualNamespace})");
            }
        }

        violations.ShouldBeEmpty(
            "Namespace must match the folder structure: {ProjectName}.{SubFolder1}.{SubFolder2}. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Asserts that isolated DbContext files in <c>*.EntityFrameworkCore</c> packages follow
    /// the canonical pattern: <c>internal sealed class</c>, primary constructor, inherits
    /// <c>GranitDbContext</c> (or calls <c>ApplyGranitConventions</c>).
    /// </summary>
    public static void DbContextClassesShouldFollowCanonicalPattern(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*DbContext.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

            string fileName = Path.GetFileName(csFile);

            if (fileName.StartsWith('I')
                || fileName.Contains("Factory", StringComparison.Ordinal)
                || fileName.Contains("Options", StringComparison.Ordinal)
                || fileName.Contains("Extensions", StringComparison.Ordinal)
                || string.Equals(fileName, "GranitDbContext.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];
            if (!moduleName.Contains("EntityFrameworkCore", StringComparison.Ordinal))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);
            string relPath = Path.GetRelativePath(repoRoot, csFile);

            if (content.Contains("public sealed class", StringComparison.Ordinal)
                || content.Contains("public class", StringComparison.Ordinal))
            {
                violations.Add($"{relPath} (must be 'internal sealed class', not 'public')");
            }

            if (content.Contains("private readonly ICurrentTenant", StringComparison.Ordinal)
                || content.Contains("private readonly IDataFilter", StringComparison.Ordinal))
            {
                violations.Add($"{relPath} (use primary constructor instead of private fields for ICurrentTenant/IDataFilter)");
            }

            bool inheritsGranitDbContext = content.Contains(": GranitDbContext", StringComparison.Ordinal);
            bool callsApplyGranitConventions = content.Contains("ApplyGranitConventions", StringComparison.Ordinal);
            if (!inheritsGranitDbContext && !callsApplyGranitConventions)
            {
                violations.Add($"{relPath} (must inherit GranitDbContext or call ApplyGranitConventions)");
            }
        }

        violations.ShouldBeEmpty(
            "Isolated DbContext classes must follow the canonical pattern: " +
            "internal sealed class, primary constructor, inheriting GranitDbContext or calling ApplyGranitConventions. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Asserts no parameterless <c>.IgnoreQueryFilters()</c> calls exist outside the allowed files.
    /// </summary>
    public static void ParameterlessIgnoreQueryFiltersShouldNotExist(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string>? allowedFileNames = null)
    {
        allowedFileNames ??= new HashSet<string>();
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            if (allowedFileNames.Contains(Path.GetFileName(csFile)))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);
            foreach (Match match in ParameterlessIgnoreQueryFilters().Matches(content))
            {
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{Path.GetRelativePath(repoRoot, csFile)}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "Parameterless .IgnoreQueryFilters() disables ALL query filters including multi-tenant isolation. " +
            "Use named filters instead: .IgnoreQueryFilters([GranitFilterNames.SoftDelete]). " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static string ComputeExpectedNamespace(
        string srcRoot,
        string filePath,
        string[] groupingFolderPrefixes)
    {
        string relativePath = Path.GetRelativePath(srcRoot, filePath);
        string[] parts = relativePath.Split(Path.DirectorySeparatorChar);

        // Strip grouping folders (e.g. "bundles/") that are not namespace segments.
        if (parts.Length > 1 && groupingFolderPrefixes.Contains(parts[0]))
        {
            parts = parts[1..];
        }

        string projectName = parts[0];

        if (projectName.EndsWith(".Abstractions", StringComparison.Ordinal))
        {
            projectName = projectName[..^".Abstractions".Length];
        }

        if (parts.Length <= 2)
        {
            return projectName;
        }

        string subfolders = string.Join('.', parts[1..^1]);
        return $"{projectName}.{subfolders}";
    }

    private static IEnumerable<string> GetSrcCsFiles(string srcDir) =>
        Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBuildOutput(f));

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    [GeneratedRegex(@"(?<=^[ \t]+(?:(?:public|private|protected|internal|static|override|sealed|virtual|new)\s+)*)async\s+void\s+\w+", RegexOptions.Multiline)]
    private static partial Regex AsyncVoidMethod();

    [GeneratedRegex(@"\bthrow\s+(?!new\b)[a-zA-Z_]\w*\s*;")]
    private static partial Regex ThrowExStatement();

    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*[;{]", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();

    /// <summary>
    /// Asserts that no file under a <c>Validators/</c> folder within <paramref name="srcDir"/>
    /// contains a hard-coded <c>.WithMessage(</c> call. Custom rules must terminate with
    /// <c>.WithErrorCodeAndMessage("...")</c> to flow through the localized resource pipeline.
    /// </summary>
    public static void NoValidatorFileShouldUseHardcodedWithMessage(string srcDir, string repoRoot)
    {
        List<string> violations = [];

        foreach (string validatorFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBuildOutput(f) && f.Contains(Path.DirectorySeparatorChar + "Validators" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)))
        {
            string content = File.ReadAllText(validatorFile);
            if (content.Contains(".WithMessage(", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(repoRoot, validatorFile));
            }
        }

        violations.ShouldBeEmpty(
            "Validator files must use .WithErrorCodeAndMessage(\"...\") instead of hard-coded " +
            ".WithMessage(\"...\") so messages flow through the localized resource pipeline. " +
            $"Violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [GeneratedRegex(@"\.IgnoreQueryFilters\(\)")]
    private static partial Regex ParameterlessIgnoreQueryFilters();
}
