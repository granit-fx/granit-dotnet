using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Prevents regressions where an endpoint in an <c>*.Endpoints</c> module explicitly
/// binds its authorization to the Identity cookie scheme, thereby bypassing the
/// <c>ForwardDefaultSelector</c> registered by <c>GranitOpenIddictModule</c> that
/// forwards cookie-auth to OpenIddict Bearer validation when an <c>Authorization</c>
/// header is present.
/// </summary>
/// <remarks>
/// <para>
/// In BFF-integrated deployments, the same backend serves multiple SPAs (e.g. a host
/// admin and a tenant app) whose session cookies may collide on <c>localhost</c> (RFC
/// 6265 — the port is not part of the cookie scope) or on a shared parent domain in
/// production. The Identity cookie is therefore effectively shared across SPAs, and
/// the last-logged-in principal wins whenever the request is authenticated via the
/// cookie scheme. The module-level forward selector mitigates this by routing API
/// requests that carry a BFF-injected <c>Authorization</c> header to OpenIddict
/// Bearer validation instead.
/// </para>
/// <para>
/// Any endpoint that declares <c>AuthenticationSchemes = IdentityConstants.ApplicationScheme</c>
/// (or the literal <c>"Identity.Application"</c>, or the default
/// <c>CookieAuthenticationDefaults.AuthenticationScheme</c>) via an
/// <c>[Authorize]</c> attribute or an inline <c>AuthorizeAttribute</c> passed to
/// <c>RequireAuthorization</c> opts out of that forwarding, so the cookie always
/// wins. That is a security regression.
/// </para>
/// <para>
/// The rule: endpoint files in <c>*.Endpoints</c> modules MUST NOT specify a
/// cookie-only <c>AuthenticationSchemes</c> value. Use bare
/// <c>.RequireAuthorization(...)</c> (inherits the default, which forwards when
/// needed) or specify the Bearer/OpenIddict validation scheme explicitly.
/// </para>
/// </remarks>
public sealed partial class CookieOnlyAuthSchemeTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Cookie-scheme names that, when used as the sole <c>AuthenticationSchemes</c>
    /// value on an endpoint, bypass the OpenIddict Bearer forwarder and re-introduce
    /// cross-frontend session leakage.
    /// </summary>
    private static readonly string[] CookieOnlySchemeTokens =
    [
        "IdentityConstants.ApplicationScheme",
        "\"Identity.Application\"",
        "CookieAuthenticationDefaults.AuthenticationScheme",
        "\"Cookies\"",
    ];

    [Fact]
    public void Endpoints_must_not_bind_authorization_to_the_Identity_cookie_scheme()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            CheckFile(csFile, content, violations);
        }

        violations.ShouldBeEmpty(
            "Endpoints MUST NOT bind their authorization to the Identity cookie scheme. " +
            "Doing so bypasses the module-level ForwardDefaultSelector that routes " +
            "BFF-injected Bearer requests to OpenIddict validation, which re-introduces " +
            "the cross-frontend session leak fixed in PR #1190. Remove the explicit " +
            "AuthenticationSchemes (falls back to the safe default) or replace it with " +
            "the OpenIddict validation scheme." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static void CheckFile(string csFile, string content, List<string> violations)
    {
        foreach (Match match in AuthenticationSchemesAssignment().Matches(content))
        {
            string rhs = match.Groups["value"].Value;

            if (!CookieOnlySchemeTokens.Any(token => rhs.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            // Accept mixed schemes that also mention Bearer/OpenIddict — those opt
            // into BOTH cookie and Bearer paths, which is harmless (the Bearer path
            // still takes precedence when the header is present).
            if (rhs.Contains("Bearer", StringComparison.OrdinalIgnoreCase)
                || rhs.Contains("OpenIddictValidationAspNetCoreDefaults", StringComparison.Ordinal)
                || rhs.Contains("OpenIddict.Validation.AspNetCore", StringComparison.Ordinal))
            {
                continue;
            }

            int lineNumber = GetLineNumber(content, match.Index);
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            violations.Add($"  {relativePath}:{lineNumber} — cookie-only scheme: {rhs.Trim()}");
        }
    }

    private static int GetLineNumber(string content, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Matches an <c>AuthenticationSchemes</c> assignment in an <c>[Authorize]</c>
    /// attribute or an inline <c>new AuthorizeAttribute { AuthenticationSchemes = ... }</c>
    /// initializer. Captures the right-hand side up to the next <c>,</c>, <c>}</c>,
    /// or <c>)</c> at brace depth 0.
    /// </summary>
    [GeneratedRegex(@"AuthenticationSchemes\s*=\s*(?<value>[^,}\)\r\n]+)")]
    private static partial Regex AuthenticationSchemesAssignment();

    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Scope the scan to *.Endpoints/ modules so we don't false-positive on
            // authentication plumbing in Granit.OpenIddict (which legitimately reads
            // the cookie scheme to configure the forwarder itself).
            string[] segments = csFile.Split(Path.DirectorySeparatorChar);
            if (!segments.Any(s => s.EndsWith(".Endpoints", StringComparison.Ordinal)))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(CookieOnlyAuthSchemeTests).Assembly.Location);
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
