using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the central authorization principle: every Minimal API endpoint must
/// declare an explicit authorization stance — either <c>.RequireAuthorization(...)</c>
/// (with or without a permission) or <c>.AllowAnonymous()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Granit's security model collapses to a single rule: the per-(user, tenant, action)
/// permission check at the endpoint boundary. If an endpoint fails to declare an auth
/// stance, the rule cannot be enforced — an anonymous caller may reach a tenant-scoped
/// resource. This test catches that drift at build time.
/// </para>
/// <para>
/// Granit endpoints commonly use a parent-child registration pattern: a registration
/// extension on <see cref="Microsoft.AspNetCore.Routing.IEndpointRouteBuilder"/> creates
/// a <c>RouteGroupBuilder</c> with <c>.RequireAuthorization(...)</c> on it, then passes
/// the group to internal <c>Map*Endpoints</c> extensions that just compose <c>MapGet</c>
/// / <c>MapPost</c> calls. The internal files contain no auth literal — the parent
/// supplied it. The test recognizes this pattern via the
/// <c>this RouteGroupBuilder</c> extension signature.
/// </para>
/// </remarks>
public sealed partial class EndpointAuthorizationConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Files known to register endpoints whose authorization is enforced by an
    /// out-of-band mechanism (protocol flow, framework convention) rather than the
    /// standard <c>RequireAuthorization</c> / <c>AllowAnonymous</c> pair.
    /// </summary>
    /// <remarks>
    /// Each entry must carry an inline justification — silent additions defeat the
    /// purpose of the allowlist.
    /// </remarks>
    private static readonly HashSet<string> Exemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // OIDC protocol endpoints. Authorization is enforced by OpenIddict's own
        // pipeline (the [Authorize] / [AllowAnonymous] split lives inside the OIDC
        // server), not by ASP.NET Core authorization policies on the route handler.
        "ConnectAuthorizationEndpoints",
        "ConnectTokenEndpoints",
        "ConnectLogoutEndpoints",
        "ConnectUserInfoEndpoints",
        "ConnectIntrospectionEndpoints",
        "ConnectRevocationEndpoints",
    };

    [Fact]
    public void Every_Map_endpoint_must_declare_authorization()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in EndpointFiles(srcDir))
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (Exemptions.Contains(fileName))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            if (!ContainsMapInvocation(content))
            {
                continue;
            }

            // Acceptable auth markers anywhere in the file:
            //   .RequireAuthorization( … )    explicit policy / permission
            //   .AllowAnonymous( )            explicit public
            if (RequireAuthorization().IsMatch(content) || AllowAnonymous().IsMatch(content))
            {
                continue;
            }

            // Callee pattern: this file is an internal composition extension whose
            // group was preauthorized by the caller (registration root). The
            // signature `this RouteGroupBuilder <param>` proves the group came in
            // from the caller — auth is the caller's responsibility, verified on
            // the caller's file by this same test.
            if (RouteGroupExtension().IsMatch(content))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            violations.Add(relativePath);
        }

        violations.ShouldBeEmpty(
            "Every Minimal API endpoint file must declare an authorization stance. "
            + "Add `.RequireAuthorization(<permission>)` or `.AllowAnonymous()` to the "
            + "endpoint chain (or to the parent group) for each Map* invocation, or — "
            + "for internal composition extensions — accept a pre-authorized "
            + "`this RouteGroupBuilder` parameter from the registration root. "
            + $"Violators: {string.Join("; ", violations)}");
    }

    private static IEnumerable<string> EndpointFiles(string srcDir)
    {
        foreach (string dir in Directory.EnumerateDirectories(srcDir))
        {
            string moduleName = Path.GetFileName(dir);
            if (!moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string csFile in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                yield return csFile;
            }
        }
    }

    private static bool ContainsMapInvocation(string content) =>
        MapInvocation().IsMatch(content);

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(EndpointAuthorizationConventionTests).Assembly.Location);
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

    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch|Methods)\s*\(", RegexOptions.Compiled)]
    private static partial Regex MapInvocation();

    [GeneratedRegex(@"\.RequireAuthorization\s*\(", RegexOptions.Compiled)]
    private static partial Regex RequireAuthorization();

    [GeneratedRegex(@"\.AllowAnonymous\s*\(", RegexOptions.Compiled)]
    private static partial Regex AllowAnonymous();

    [GeneratedRegex(@"\bthis\s+RouteGroupBuilder\b", RegexOptions.Compiled)]
    private static partial Regex RouteGroupExtension();
}
