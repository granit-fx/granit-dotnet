using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Conventions for the <c>Granit.Http.SecurityHeaders</c> contributor pattern:
/// <list type="bullet">
/// <item><c>ICspContributor</c> implementations must be <c>sealed</c>.</item>
/// <item><c>ICspContributor</c> implementations must live in an
///   <c>Internal/</c> folder of their owning <c>src/</c> package —
///   contributors are discovery-only types, never part of any package's
///   public API surface.</item>
/// </list>
/// </summary>
public sealed partial class SecurityHeadersConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Csp_contributors_must_be_sealed_and_live_in_Internal_folder()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> nonSealed = [];
        List<string> nonInternal = [];

        foreach (string csFile in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);
            Match match = ImplementsCspContributor().Match(content);
            if (!match.Success)
            {
                continue;
            }

            // Skip the interface definition itself + the registry impl that
            // exposes contributors but isn't one.
            if (csFile.EndsWith("ICspContributor.cs", StringComparison.Ordinal)
                || csFile.EndsWith("CspContributorRegistry.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string relative = Path.GetRelativePath(srcDir, csFile);

            if (!SealedClassImplementsCspContributor().IsMatch(content))
            {
                nonSealed.Add(relative);
            }

            string sep = Path.DirectorySeparatorChar.ToString();
            if (!csFile.Contains(sep + "Internal" + sep, StringComparison.Ordinal))
            {
                nonInternal.Add(relative);
            }
        }

        nonSealed.ShouldBeEmpty(
            "ICspContributor implementations must be `sealed` — they are " +
            "instantiated via DI; inheritance is not part of the contract. " +
            $"Violators: {string.Join(", ", nonSealed)}");

        nonInternal.ShouldBeEmpty(
            "ICspContributor implementations must reside in an Internal/ " +
            "folder of their owning package — they are discovery-only types, " +
            "never part of the package's public API surface. " +
            $"Violators: {string.Join(", ", nonInternal)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(SecurityHeadersConventionTests).Assembly.Location);
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
    /// Matches any class declaration that implements <c>ICspContributor</c>
    /// (with or without other interfaces in the implements list). Tolerates
    /// modifiers (sealed/internal/public/etc.) in any order.
    /// </summary>
    [GeneratedRegex(@"\bclass\s+\w+\s*:\s*[^{]*\bICspContributor\b", RegexOptions.Multiline)]
    private static partial Regex ImplementsCspContributor();

    /// <summary>
    /// Matches a class declaration that is both <c>sealed</c> and implements
    /// <c>ICspContributor</c>.
    /// </summary>
    [GeneratedRegex(
        @"(?:\b\w+\b\s+)*\bsealed\b(?:\s+\b\w+\b)*\s+class\s+\w+\s*:\s*[^{]*\bICspContributor\b",
        RegexOptions.Multiline)]
    private static partial Regex SealedClassImplementsCspContributor();
}
