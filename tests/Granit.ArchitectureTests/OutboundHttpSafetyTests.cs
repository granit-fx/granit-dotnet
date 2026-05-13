using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules pinning the SSRF defense posture across the framework. These tests
/// guard the layered defense (validator + connect-callback + reserved-IP classifier) against
/// regressions when new modules emit outbound HTTP or webhook traffic.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>R-SSRF-1:</b> any package emitting outbound user-controlled URLs MUST reference
///   <c>Granit.Http.Security</c>.</item>
///   <item><b>R-SSRF-3:</b> no source code may call <c>Dns.GetHostEntry</c> / <c>GetHostEntryAsync</c>
///   (triggers reverse-PTR; use <c>GetHostAddressesAsync</c> instead).</item>
/// </list>
/// R-SSRF-2 (forbid raw <c>SocketsHttpHandler</c> without <c>ConnectCallback</c>) and R-SSRF-4
/// (force <c>--host-resolver-rules</c> in browser launch args) are documented but not enforced
/// here — pattern-matching them across the codebase produces too many false positives.
/// </remarks>
public sealed class OutboundHttpSafetyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// R-SSRF-1 — packages that emit outbound user-controlled URLs must reference
    /// <c>Granit.Http.Security</c>. The reference makes the SSRF blocklist and
    /// <c>IUrlSafetyValidator</c> available without each consumer reinventing the rule set.
    /// </summary>
    /// <remarks>
    /// Provider / endpoint sub-packages (<c>*.Playwright</c>, <c>*.PuppeteerSharp</c>,
    /// <c>*.Endpoints</c>) inherit the reference transitively through their base package and are
    /// not listed here.
    /// </remarks>
    [Theory]
    [InlineData("Granit.Webhooks")]
    [InlineData("Granit.Browsing")]
    public void Outbound_user_url_packages_must_reference_Granit_Http_Security(string projectName)
    {
        string csprojPath = Path.Join(RepoRoot, "src", projectName, $"{projectName}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        bool referenced = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Any(r => r.Contains("Granit.Http.Security.csproj", StringComparison.Ordinal)
                  && !r.Contains("Granit.Http.SecurityHeaders", StringComparison.Ordinal));

        referenced.ShouldBeTrue(
            $"{projectName} emits outbound user-controlled URLs and must reference Granit.Http.Security " +
            $"so it can use IUrlSafetyValidator / PrivateNetworkClassifier instead of inventing local rules.");
    }

    /// <summary>
    /// R-SSRF-3 — forbid <c>Dns.GetHostEntry</c> / <c>GetHostEntryAsync</c>. These also perform
    /// a reverse-PTR lookup (slow, can hang on broken reverse zones, leaks internal hostnames over
    /// DNS). Use <c>Dns.GetHostAddressesAsync(host, ct)</c> instead.
    /// </summary>
    [Fact]
    public void No_source_should_call_Dns_GetHostEntry()
    {
        List<string> violations = [];

        foreach (string csFile in EnumerateRepoCsFiles())
        {
            // Skip test code (allowed to call any DNS API for assertions / fakes).
            if (csFile.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);
            if (content.Contains("Dns.GetHostEntry", StringComparison.Ordinal))
            {
                string rel = Path.GetRelativePath(RepoRoot, csFile);
                violations.Add(rel);
            }
        }

        violations.ShouldBeEmpty(
            "Dns.GetHostEntry / GetHostEntryAsync triggers reverse-PTR lookups (slow, leaky). " +
            "Use Dns.GetHostAddressesAsync(host, ct) for forward-only A/AAAA resolution. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static IEnumerable<string> EnumerateRepoCsFiles()
    {
        foreach (string root in (string[])["src", "tests"])
        {
            string dir = Path.Join(RepoRoot, root);
            if (!Directory.Exists(dir))
            {
                continue;
            }
            foreach (string cs in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                // Skip generated obj/bin output.
                if (cs.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || cs.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }
                yield return cs;
            }
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(OutboundHttpSafetyTests).Assembly.Location);
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
}
