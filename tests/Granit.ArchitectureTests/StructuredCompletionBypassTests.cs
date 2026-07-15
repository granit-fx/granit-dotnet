using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Governance test for ADR-064 (#2452, phase 4). A <c>.AI</c> module must produce typed output
/// through the <see cref="Granit.AI.IStructuredCompletion"/> primitive — never by hand-rolling the
/// bypass: <c>IChatClient.GetResponseAsync(...)</c> followed by a manual
/// <c>JsonSerializer.Deserialize&lt;T&gt;(...)</c>. The bypass skips provider-enforced JSON-schema
/// pinning, the four-valued status, usage tracking, and the prompt-injection isolation the
/// primitive applies — re-introducing the fragmentation ADR-064 removed.
/// </summary>
/// <remarks>
/// File-level heuristic: a source file is a violator when it contains BOTH a
/// <c>GetResponseAsync</c>/<c>GetStreamingResponseAsync</c> call AND a
/// <c>JsonSerializer.Deserialize</c> call. Modules that call <c>GetResponseAsync</c> for raw text
/// (Templating, OCR, Timeline summarizer) never deserialize, so they are not caught. Legitimate
/// exceptions are listed in <see cref="AllowedBypassFiles"/> with an inline justification.
/// </remarks>
public sealed partial class StructuredCompletionBypassTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Files allowed to use the <c>GetResponseAsync</c> + <c>Deserialize</c> bypass, keyed by their
    /// path relative to <c>src/</c> (forward-slashed). Each entry MUST carry an inline justification.
    /// </summary>
    private static readonly HashSet<string> AllowedBypassFiles = new(StringComparer.Ordinal)
    {
        // The IStructuredCompletion implementation itself — it owns GetResponseAsync + Deserialize<T>
        // precisely so every other .AI module does not have to (ADR-064).
        "Granit.AI/Internal/DefaultStructuredCompletion.cs",
    };

    [Fact]
    public void AI_modules_must_route_typed_output_through_IStructuredCompletion()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativeToSrc = Path.GetRelativePath(srcDir, csFile).Replace(Path.DirectorySeparatorChar, '/');

            // Only police .AI modules — the bypass fragmentation ADR-064 fixed is an AI concern.
            string moduleName = relativeToSrc.Split('/')[0];
            if (!moduleName.EndsWith(".AI", StringComparison.Ordinal))
            {
                continue;
            }

            if (AllowedBypassFiles.Contains(relativeToSrc))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            if (GetResponseCall().IsMatch(content) && JsonDeserializeCall().IsMatch(content))
            {
                violations.Add(relativeToSrc);
            }
        }

        violations.ShouldBeEmpty(
            "A .AI module produced typed output via the bypass (IChatClient.GetResponseAsync + manual "
            + "JsonSerializer.Deserialize) instead of IStructuredCompletion.CompleteAsync<T> (ADR-064). "
            + "Route the call through the primitive, or — if the use case genuinely cannot (e.g. multimodal "
            + "input) — add the file to AllowedBypassFiles with an inline justification. "
            + $"Violators: {string.Join(", ", violations)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(StructuredCompletionBypassTests).Assembly.Location);
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

    /// <summary>Matches a call to <c>.GetResponseAsync(</c> or <c>.GetStreamingResponseAsync(</c>.</summary>
    [GeneratedRegex(@"\.Get(?:Streaming)?ResponseAsync\s*\(")]
    private static partial Regex GetResponseCall();

    /// <summary>Matches a call to <c>JsonSerializer.Deserialize</c> (generic or not).</summary>
    [GeneratedRegex(@"\bJsonSerializer\.Deserialize\b")]
    private static partial Regex JsonDeserializeCall();
}
