using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that minimal API handlers bound to bodyless HTTP verbs
/// (<c>DELETE</c>, <c>GET</c>, <c>HEAD</c>) carry an explicit <c>[FromBody]</c>
/// on any complex request DTO parameter.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core 10 tightened the minimal-API parameter binder: a complex parameter
/// inferred as <c>[FromBody]</c> on a verb that the RFC defines as bodyless
/// (DELETE / GET / HEAD) is rejected at endpoint construction with:
/// </para>
/// <code>
/// System.InvalidOperationException: Body was inferred but the method does not allow inferred body parameters.
/// </code>
/// <para>
/// The failure happens at app startup (first request hitting the routing middleware),
/// so a missing <c>[FromBody]</c> on these verbs is a deployment-time regression
/// that escapes compile-time checks.
/// </para>
/// </remarks>
public sealed partial class BodylessVerbBodyBindingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Granit convention (see <c>CLAUDE.md</c> / DTO conventions): every body-shaped
    /// input DTO ends with the <c>Request</c> suffix (never <c>*Dto</c>, never
    /// <c>*Command</c>). A parameter whose type ends with this suffix on a
    /// <c>DELETE</c>/<c>GET</c>/<c>HEAD</c> handler is therefore always a body
    /// parameter and must be annotated explicitly.
    /// </summary>
    private const string BodyDtoSuffix = "Request";

    private static readonly HashSet<string> ExplicitBindingAttributes = new(StringComparer.Ordinal)
    {
        "FromBody",
        "FromServices",
        "FromKeyedServices",
        "FromQuery",
        "FromRoute",
        "FromHeader",
        "FromForm",
        "AsParameters",
    };

    [Fact]
    public void Bodyless_verb_handlers_must_annotate_request_dto_parameters_with_FromBody()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string[] lines = File.ReadAllLines(csFile);
            CheckFile(csFile, lines, violations);
        }

        violations.ShouldBeEmpty(
            "Minimal API handlers bound to DELETE/GET/HEAD must declare an explicit " +
            "[FromBody] (or another explicit [From*]) attribute on any *Request DTO parameter. " +
            "Without it, ASP.NET Core 10 throws 'Body was inferred but the method does not allow " +
            "inferred body parameters' at endpoint construction, breaking app startup." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static void CheckFile(string csFile, string[] lines, List<string> violations)
    {
        HashSet<string> bodylessHandlerNames = CollectBodylessHandlerReferences(lines);
        if (bodylessHandlerNames.Count == 0)
        {
            return;
        }

        string relativePath = Path.GetRelativePath(RepoRoot, csFile);

        for (int i = 0; i < lines.Length; i++)
        {
            Match decl = MethodDeclarationPattern().Match(lines[i]);
            if (!decl.Success)
            {
                continue;
            }

            string methodName = decl.Groups["name"].Value;
            if (!bodylessHandlerNames.Contains(methodName))
            {
                continue;
            }

            ValidateHandlerParameters(relativePath, lines, methodStartLine: i, methodName, violations);
        }
    }

    /// <summary>
    /// First pass: find every <c>Map(Delete|Get|Head)("...", HandlerIdentifier)</c>
    /// invocation and collect the handler identifier. Lambda handlers are skipped
    /// — they would need full Roslyn parsing to extract param lists reliably and
    /// are rare in the Granit codebase (extension methods on <c>RouteGroupBuilder</c>
    /// are the convention).
    /// </summary>
    private static HashSet<string> CollectBodylessHandlerReferences(string[] lines)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            foreach (Match m in BodylessMapInvocationPattern().Matches(line))
            {
                result.Add(m.Groups["handler"].Value);
            }
        }

        return result;
    }

    /// <summary>
    /// Walks the parameter list of the handler method (multi-line) and reports
    /// any parameter whose declared type ends in <c>Request</c> but is not
    /// preceded by an explicit binding attribute.
    /// </summary>
    private static void ValidateHandlerParameters(
        string relativePath,
        string[] lines,
        int methodStartLine,
        string methodName,
        List<string> violations)
    {
        // Walk lines until we close the param list (paren depth returns to zero).
        int depth = 0;
        bool started = false;
        for (int i = methodStartLine; i < lines.Length; i++)
        {
            string line = lines[i];
            foreach (char c in line)
            {
                if (c == '(')
                {
                    depth++;
                    started = true;
                }
                else if (c == ')')
                {
                    depth--;
                }
            }

            if (started && depth == 0)
            {
                // Past the closing paren — stop scanning.
                CheckParamLine(relativePath, lines, i, methodName, violations);
                return;
            }

            // Mid-list parameter line.
            if (started)
            {
                CheckParamLine(relativePath, lines, i, methodName, violations);
            }
        }
    }

    private static void CheckParamLine(
        string relativePath,
        string[] lines,
        int lineIndex,
        string methodName,
        List<string> violations)
    {
        string line = lines[lineIndex];
        Match paramMatch = RequestParamPattern().Match(line);
        if (!paramMatch.Success)
        {
            return;
        }

        string typeName = paramMatch.Groups["type"].Value;

        if (HasExplicitBindingAttribute(line))
        {
            return;
        }

        // Also accept an attribute on the preceding non-blank line (the existing
        // convention test allows both shapes; we follow suit for consistency).
        for (int j = lineIndex - 1; j >= Math.Max(0, lineIndex - 3); j--)
        {
            string prev = lines[j].TrimStart();
            if (string.IsNullOrWhiteSpace(prev))
            {
                continue;
            }

            if (HasExplicitBindingAttribute(prev))
            {
                return;
            }

            break;
        }

        violations.Add(
            $"  {relativePath}:{lineIndex + 1} — '{methodName}' parameter of type '{typeName}' " +
            "needs [FromBody] (bodyless verb handler).");
    }

    private static bool HasExplicitBindingAttribute(string line) =>
        ExplicitBindingAttributes.Any(attr =>
            line.Contains($"[{attr}]", StringComparison.Ordinal) ||
            line.Contains($"[{attr}(", StringComparison.Ordinal) ||
            line.Contains($"[{attr} ", StringComparison.Ordinal) ||
            line.Contains($"[{attr},", StringComparison.Ordinal));

    /// <summary>
    /// Matches <c>Map(Delete|Get|Head)("…route…", HandlerIdentifier)</c> where
    /// the second argument is a bare identifier (method-group reference). Lambdas
    /// are intentionally excluded.
    /// </summary>
    [GeneratedRegex(@"\bMap(?:Delete|Get|Head)\s*\(\s*""[^""]*""\s*,\s*(?<handler>[A-Za-z_][\w]*)\s*\)")]
    private static partial Regex BodylessMapInvocationPattern();

    /// <summary>
    /// Matches a method declaration line — captures the method name. Conservative:
    /// matches <c>private|internal|public|protected (static)? (async)? Task[&lt;...&gt;] Name(</c>
    /// or expression-bodied equivalents. We accept partial matches and rely on
    /// the handler-name set built in pass 1 to filter to real handlers.
    /// </summary>
    [GeneratedRegex(@"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:async\s+)?[\w<>,\s\.\?\[\]]+?\s+(?<name>[A-Z][\w]*)\s*\(")]
    private static partial Regex MethodDeclarationPattern();

    /// <summary>
    /// Matches a single parameter line whose type ends in <c>Request</c>
    /// (e.g. <c>BlobDeleteRequest request,</c> or <c>RevokePublicLinkRequest? request,</c>).
    /// Captures the type for diagnostics.
    /// </summary>
    [GeneratedRegex(@"(?<type>[A-Z][\w]*" + BodyDtoSuffix + @")\??\s+\w+\s*[,)]")]
    private static partial Regex RequestParamPattern();

    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (!fileName.Contains("Endpoint", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(BodylessVerbBodyBindingTests).Assembly.Location);
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
