using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rule: every Minimal API endpoint file must declare an explicit authorization stance
/// — either <c>.RequireAuthorization(...)</c> or <c>.AllowAnonymous()</c>.
/// </summary>
public static partial class EndpointAuthorizationRules
{
    /// <summary>
    /// Every C# file in <c>*.Endpoints</c> directories that contains a <c>Map*</c> invocation
    /// must declare either <c>.RequireAuthorization(...)</c> or <c>.AllowAnonymous()</c>.
    /// Files whose authorization is inherited from a parent <c>RouteGroupBuilder</c>
    /// (<c>this RouteGroupBuilder</c> extension signature) are exempt.
    /// </summary>
    /// <param name="srcDir">Path to the repo's <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root — used to compute relative paths in violation messages.</param>
    /// <param name="exemptFileNames">
    /// Additional filenames (without extension, case-insensitive) whose auth is managed
    /// by an out-of-band mechanism (e.g. OIDC protocol endpoints).
    /// </param>
    public static void EveryEndpointMustDeclareAuthorizationStance(
        string srcDir,
        string repoRoot,
        params string[] exemptFileNames)
    {
        HashSet<string> exemptions = new(exemptFileNames, StringComparer.OrdinalIgnoreCase);
        List<string> violations = [];

        foreach (string csFile in GetEndpointFiles(srcDir))
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (exemptions.Contains(fileName))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            if (!ContainsMapInvocation(content))
            {
                continue;
            }

            if (RequireAuthorization().IsMatch(content) || AllowAnonymous().IsMatch(content))
            {
                continue;
            }

            // Callee pattern: file is an internal composition extension whose group
            // was pre-authorized by the caller. Auth is the caller's responsibility.
            if (RouteGroupExtension().IsMatch(content))
            {
                continue;
            }

            violations.Add(Path.GetRelativePath(repoRoot, csFile));
        }

        violations.ShouldBeEmpty(
            "Every Minimal API endpoint file must declare an authorization stance. " +
            "Add .RequireAuthorization(<permission>) or .AllowAnonymous() to the endpoint chain " +
            "(or to the parent group), or accept a pre-authorized `this RouteGroupBuilder` param. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// No C# file under <paramref name="roots"/> may call <c>RequireRole</c>. Granit authorization is
    /// permission-based end to end: production endpoints gate on <c>RequireAuthorization(SomePermission)</c>
    /// and tests grant permissions (see <c>TestAuthHandler.PermissionsHeader</c>) — never roles. A role-based
    /// policy in a test gives false confidence: it passes while exercising a mechanism production never uses.
    /// </summary>
    /// <param name="repoRoot">Repo root — used to compute relative paths in violation messages.</param>
    /// <param name="roots">Directories to scan recursively (e.g. <c>src</c> and <c>tests</c>).</param>
    public static void RequireRoleMustNotBeUsed(string repoRoot, params string[] roots)
    {
        List<string> violations = [];

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string csFile in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                if (RequireRole().IsMatch(File.ReadAllText(csFile)))
                {
                    violations.Add(Path.GetRelativePath(repoRoot, csFile));
                }
            }
        }

        violations.ShouldBeEmpty(
            "Authorization must be permission-based, never role-based: no RequireRole calls anywhere, " +
            "including tests. Gate endpoints on RequireAuthorization(<permission>); in tests, grant the " +
            "permission via TestAuthHandler.PermissionsHeader and match it with " +
            "RequireClaim(TestAuthHandler.PermissionClaimType, <permission>). " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static IEnumerable<string> GetEndpointFiles(string srcDir)
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

    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch|Methods)\s*\(", RegexOptions.Compiled)]
    private static partial Regex MapInvocation();

    [GeneratedRegex(@"\.RequireAuthorization\s*\(", RegexOptions.Compiled)]
    private static partial Regex RequireAuthorization();

    [GeneratedRegex(@"\.AllowAnonymous\s*\(", RegexOptions.Compiled)]
    private static partial Regex AllowAnonymous();

    [GeneratedRegex(@"\bthis\s+RouteGroupBuilder\b", RegexOptions.Compiled)]
    private static partial Regex RouteGroupExtension();

    [GeneratedRegex(@"\.RequireRole\s*\(", RegexOptions.Compiled)]
    private static partial Regex RequireRole();
}
