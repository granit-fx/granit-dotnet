using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Ensures all caching goes through <c>Granit.Caching</c> (<c>IFusionCache</c> / <c>IConditionalCache</c>).
/// No module should use raw <c>IDistributedCache</c> or <c>IMemoryCache</c> directly.
/// </summary>
public sealed partial class CachingConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Projects that are allowed to reference raw cache interfaces
    /// (infrastructure that wraps them for the rest of the framework).
    /// </summary>
    private static readonly string[] ExcludedProjects =
    [
        "Granit.Caching",
        "Granit.Caching.StackExchangeRedis",
        "Granit.Http.OutputCaching.StackExchangeRedis",
        "Granit.BlobStorage.Proxy", // Ephemeral single-use proxy tokens — IDistributedCache is the consumer contract
        "Granit.Bff.EntityFrameworkCore", // EF-based alternative to cache-based BFF token store
        "Granit.Localization", // IMemoryCache registered for Microsoft's JsonStringLocalizerFactory
    ];

    [Fact]
    public void Modules_should_not_inject_IDistributedCache_directly()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcludedPath(csFile))
            {
                continue;
            }

            string content = StripComments(File.ReadAllText(csFile));

            foreach (Match match in IDistributedCacheUsage().Matches(content))
            {
                string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "IDistributedCache must not be used directly — use IFusionCache (from Granit.Caching) " +
            "or IConditionalCache for atomic operations. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Modules_should_not_inject_IMemoryCache_directly()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcludedPath(csFile))
            {
                continue;
            }

            string content = StripComments(File.ReadAllText(csFile));

            foreach (Match match in IMemoryCacheUsage().Matches(content))
            {
                string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "IMemoryCache must not be used directly — use IFusionCache (from Granit.Caching) instead. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Removes C# comments (line, XML doc, and block) so prose mentions of a cache
    /// interface aren't mistaken for real usage. Newlines inside block comments are
    /// preserved so reported line numbers stay accurate.
    /// </summary>
    private static string StripComments(string content)
    {
        content = BlockComment().Replace(content, match => NonLineFeed().Replace(match.Value, string.Empty));
        return LineComment().Replace(content, string.Empty);
    }

    private static bool IsExcludedPath(string csFile)
    {
        string sep = Path.DirectorySeparatorChar.ToString();

        if (csFile.Contains(sep + "bin" + sep) || csFile.Contains(sep + "obj" + sep))
        {
            return true;
        }

        return ExcludedProjects.Any(project => csFile.Contains(sep + project + sep));
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir)!;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    [GeneratedRegex(@"\bIDistributedCache\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IDistributedCacheUsage();

    [GeneratedRegex(@"\bIMemoryCache\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IMemoryCacheUsage();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineComment();

    [GeneratedRegex(@"[^\n]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonLineFeed();
}
