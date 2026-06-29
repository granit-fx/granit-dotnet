using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable HTTP response and OpenAPI convention rules for Minimal API endpoints.
/// </summary>
public static partial class ApiConventionRules
{
    /// <summary>
    /// Endpoint handler methods returning <c>Task&lt;IResult&gt;</c> or bare <c>IResult</c>
    /// should use <c>Results&lt;...&gt;</c> union types for compile-time type safety and
    /// automatic OpenAPI response documentation.
    /// </summary>
    public static void EndpointHandlersShouldUseTypedResults(
        string srcDir,
        string repoRoot,
        params string[] exemptFileNames)
    {
        HashSet<string> exemptions = new(exemptFileNames, StringComparer.OrdinalIgnoreCase);
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (exemptions.Contains(fileName))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            foreach (Match match in BareIResultReturn().Matches(content))
            {
                string relativePath = Path.GetRelativePath(repoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber} ({match.Value.Trim()})");
            }
        }

        violations.ShouldBeEmpty(
            "Endpoint handlers should return Results<...> union types instead of Task<IResult> or IResult. " +
            "Union types provide compile-time type safety and automatic OpenAPI response documentation. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// <c>Results&lt;...&gt;</c> union types with 3+ type arguments that include error-related
    /// types should also include <c>ProblemHttpResult</c> for unexpected error paths.
    /// Two-type unions are exempt (rely on exception-handler middleware).
    /// </summary>
    public static void ComplexResultsUnionsShouldIncludeProblemHttpResult(
        string srcDir,
        string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);

            foreach (Match match in ResultsUnionType().Matches(content))
            {
                string unionArgs = match.Groups[1].Value;

                if (unionArgs.Contains("ProblemHttpResult", StringComparison.Ordinal))
                {
                    continue;
                }

                if (CountTypeArguments(unionArgs) <= 2)
                {
                    continue;
                }

                if (AllTypesAreSimple(unionArgs))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(repoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber} (Results<{unionArgs}>)");
            }
        }

        violations.ShouldBeEmpty(
            "Results<...> union types with 3+ type arguments should include ProblemHttpResult " +
            "to document error responses in OpenAPI. Two-type unions are exempt. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Every endpoint registration (<c>.MapGet()</c>, <c>.MapPost()</c>, etc.) in
    /// <c>*.Endpoints</c> packages must declare <c>.WithName()</c>, <c>.WithSummary()</c>,
    /// <c>.WithDescription()</c>, and at least one <c>.Produces()</c> variant.
    /// </summary>
    public static void EndpointRegistrationsShouldHaveCompleteOpenApiMetadata(
        string srcDir,
        string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetEndpointPackageSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            string relativePath = Path.GetRelativePath(repoRoot, csFile);

            foreach (Match match in EndpointRegistration().Matches(content))
            {
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                string chain = ExtractFluentChain(content, match.Index);

                if (chain.Contains(".ExcludeFromDescription(", StringComparison.Ordinal))
                {
                    continue;
                }

                List<string> missing = [];
                if (!chain.Contains(".WithName(", StringComparison.Ordinal))
                {
                    missing.Add("WithName");
                }

                if (!chain.Contains(".WithSummary(", StringComparison.Ordinal))
                {
                    missing.Add("WithSummary");
                }

                if (!chain.Contains(".WithDescription(", StringComparison.Ordinal))
                {
                    missing.Add("WithDescription");
                }

                if (!chain.Contains(".Produces", StringComparison.Ordinal))
                {
                    missing.Add("Produces");
                }

                if (missing.Count > 0)
                {
                    violations.Add(
                        $"{relativePath}:{lineNumber} (.Map{match.Groups[1].Value}) missing: {string.Join(", ", missing)}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every endpoint registration must declare WithName, WithSummary, WithDescription, " +
            "and at least one Produces variant for OpenAPI documentation. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Public <c>Map*</c> extension methods on <c>IEndpointRouteBuilder</c> must follow
    /// a <c>Map{Prefix}{Feature}()</c> naming convention to avoid collisions with
    /// third-party or ASP.NET Core extensions.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative path messages.</param>
    /// <param name="requiredPrefix">
    /// Required prefix after "Map" (e.g. <c>"Granit"</c> → enforces <c>MapGranit*</c>).
    /// </param>
    /// <param name="exemptMethodNames">Method names that are intentionally exempt.</param>
    public static void PublicMapExtensionMethodsShouldFollowNamingConvention(
        string srcDir,
        string repoRoot,
        string requiredPrefix,
        params string[] exemptMethodNames)
    {
        HashSet<string> exemptions = new(exemptMethodNames, StringComparer.Ordinal);
        List<string> violations = [];

        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            foreach (Match match in PublicMapExtensionMethod().Matches(content))
            {
                string methodName = match.Groups[1].Value;

                if (exemptions.Contains(methodName))
                {
                    continue;
                }

                if (!methodName.StartsWith($"Map{requiredPrefix}", StringComparison.Ordinal))
                {
                    string relativePath = Path.GetRelativePath(repoRoot, csFile);
                    int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                    violations.Add($"{relativePath}:{lineNumber} ({methodName}) — expected Map{requiredPrefix}{{Feature}}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Public Map* extension methods on IEndpointRouteBuilder must follow " +
            $"the Map{requiredPrefix}{{Feature}}() naming convention. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Every <c>.ProducesValidationProblem(...)</c> OpenAPI annotation must declare
    /// <c>422 Unprocessable Entity</c> explicitly. A bare <c>.ProducesValidationProblem()</c>
    /// defaults to <c>400</c> in the generated document while the runtime
    /// <c>FluentValidationAutoEndpointFilter</c> returns <c>422</c> — the drift that ships a
    /// phantom <c>400</c> into the OpenAPI snapshots that no validation failure ever returns.
    /// <c>400</c> stays reserved for malformed requests and domain errors carrying an error code.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative path messages.</param>
    public static void ProducesValidationProblemShouldTarget422(
        string srcDir,
        string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);

            foreach (Match match in ProducesValidationProblemCall().Matches(content))
            {
                string args = match.Groups["args"].Value;
                if (args.Contains("Status422UnprocessableEntity", StringComparison.Ordinal)
                    || args.Contains("422", StringComparison.Ordinal))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(repoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add(
                    $"{relativePath}:{lineNumber} — .ProducesValidationProblem() defaults to 400; " +
                    "declare .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).");
            }
        }

        violations.ShouldBeEmpty(
            "Every .ProducesValidationProblem(...) must target 422 to match the runtime " +
            "FluentValidationAutoEndpointFilter, otherwise the generated OpenAPI document " +
            "advertises a 400 that no validation failure ever returns. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Minimal API handlers bound to bodyless HTTP verbs (<c>DELETE</c>, <c>GET</c>, <c>HEAD</c>)
    /// must carry an explicit <c>[FromBody]</c> (or another explicit <c>[From*]</c>) on any complex
    /// <c>*Request</c> DTO parameter. ASP.NET Core 10 rejects an inferred body parameter on these
    /// verbs at endpoint construction (<c>InvalidOperationException: Body was inferred but the
    /// method does not allow inferred body parameters</c>), failing app startup — a regression that
    /// escapes compile-time checks.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative path messages.</param>
    public static void BodylessVerbHandlersShouldAnnotateRequestDtoParameters(
        string srcDir,
        string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string[] lines = File.ReadAllLines(csFile);
            CheckBodylessFile(csFile, repoRoot, lines, violations);
        }

        violations.ShouldBeEmpty(
            "Minimal API handlers bound to DELETE/GET/HEAD must declare an explicit " +
            "[FromBody] (or another explicit [From*]) attribute on any *Request DTO parameter. " +
            "Without it, ASP.NET Core 10 throws 'Body was inferred but the method does not allow " +
            "inferred body parameters' at endpoint construction, breaking app startup. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static IEnumerable<string> GetEndpointPackageSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                yield return csFile;
            }
        }
    }

    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (moduleName.Contains("Endpoints", StringComparison.Ordinal)
                || csFile.Contains(Path.DirectorySeparatorChar + "Endpoints" + Path.DirectorySeparatorChar)
                || csFile.Contains("Endpoint", StringComparison.Ordinal))
            {
                yield return csFile;
            }
        }
    }

    private static string ExtractFluentChain(string content, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < content.Length; i++)
        {
            char c = content[i];
            switch (c)
            {
                case '(' or '{': depth++; break;
                case ')' or '}': depth--; break;
                case ';' when depth <= 0: return content[startIndex..i];
            }
        }
        return content[startIndex..];
    }

    private static int CountTypeArguments(string typeArgs)
    {
        int count = 1, depth = 0;
        foreach (char c in typeArgs)
        {
            switch (c)
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case ',' when depth == 0: count++; break;
            }
        }
        return count;
    }

    private static bool AllTypesAreSimple(string typeArgs)
    {
        List<string> types = [];
        int depth = 0, start = 0;
        for (int i = 0; i <= typeArgs.Length; i++)
        {
            if (i == typeArgs.Length || (typeArgs[i] == ',' && depth == 0))
            {
                string part = typeArgs[start..i].Trim();
                int angle = part.IndexOf('<');
                types.Add(angle >= 0 ? part[..angle].Trim() : part);
                start = i + 1;
            }
            else if (typeArgs[i] == '<')
            {
                depth++;
            }
            else if (typeArgs[i] == '>')
            {
                depth--;
            }
        }

        HashSet<string> simple = new(StringComparer.Ordinal)
        {
            "Ok", "Created", "Accepted", "NoContent",
            "NotFound", "ValidationProblem", "FileStreamHttpResult",
            "UnauthorizedHttpResult", "ForbidResult",
        };
        return types.All(t => simple.Contains(t));
    }

    /// <summary>
    /// Granit convention: every body-shaped input DTO ends with the <c>Request</c> suffix. A
    /// parameter whose type ends with this suffix on a <c>DELETE</c>/<c>GET</c>/<c>HEAD</c> handler
    /// is therefore always a body parameter and must be annotated explicitly.
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

    private static void CheckBodylessFile(string csFile, string repoRoot, string[] lines, List<string> violations)
    {
        HashSet<string> bodylessHandlerNames = CollectBodylessHandlerReferences(lines);
        if (bodylessHandlerNames.Count == 0)
        {
            return;
        }

        string relativePath = Path.GetRelativePath(repoRoot, csFile);

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

            ValidateBodylessHandlerParameters(relativePath, lines, methodStartLine: i, methodName, violations);
        }
    }

    /// <summary>
    /// First pass: find every <c>Map(Delete|Get|Head)("...", HandlerIdentifier)</c> invocation and
    /// collect the handler identifier. Lambda handlers are skipped — they would need full Roslyn
    /// parsing to extract param lists reliably and are rare in the Granit codebase.
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
    /// Walks the parameter list of the handler method (multi-line) and reports any parameter whose
    /// declared type ends in <c>Request</c> but is not preceded by an explicit binding attribute.
    /// </summary>
    private static void ValidateBodylessHandlerParameters(
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
            foreach (char c in lines[i])
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
                CheckBodylessParamLine(relativePath, lines, i, methodName, violations);
                return;
            }

            // Mid-list parameter line.
            if (started)
            {
                CheckBodylessParamLine(relativePath, lines, i, methodName, violations);
            }
        }
    }

    private static void CheckBodylessParamLine(
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

        // Also accept an attribute on the preceding non-blank line (both shapes are allowed).
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
            $"{relativePath}:{lineIndex + 1} — '{methodName}' parameter of type '{typeName}' " +
            "needs [FromBody] (bodyless verb handler).");
    }

    private static bool HasExplicitBindingAttribute(string line) =>
        ExplicitBindingAttributes.Any(attr =>
            line.Contains($"[{attr}]", StringComparison.Ordinal) ||
            line.Contains($"[{attr}(", StringComparison.Ordinal) ||
            line.Contains($"[{attr} ", StringComparison.Ordinal) ||
            line.Contains($"[{attr},", StringComparison.Ordinal));

    [GeneratedRegex(@"(?:Task<IResult>|IResult)\s+\w+Async?\s*\(", RegexOptions.Multiline)]
    private static partial Regex BareIResultReturn();

    [GeneratedRegex(@"Results<((?:[^<>]|<[^<>]*>)+)>\s*>\s+\w+", RegexOptions.Multiline)]
    private static partial Regex ResultsUnionType();

    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch)\s*\(", RegexOptions.Multiline)]
    private static partial Regex EndpointRegistration();

    [GeneratedRegex(@"\.ProducesValidationProblem\s*\(\s*(?<args>[^)]*)\)", RegexOptions.Multiline)]
    private static partial Regex ProducesValidationProblemCall();

    /// <summary>
    /// Matches <c>Map(Delete|Get|Head)("…route…", HandlerIdentifier)</c> where the second argument
    /// is a bare identifier (method-group reference). Lambdas are intentionally excluded.
    /// </summary>
    [GeneratedRegex(@"\bMap(?:Delete|Get|Head)\s*\(\s*""[^""]*""\s*,\s*(?<handler>[A-Za-z_][\w]*)\s*\)")]
    private static partial Regex BodylessMapInvocationPattern();

    /// <summary>
    /// Matches a method declaration line — captures the method name. Conservative; we accept
    /// partial matches and rely on the handler-name set built in pass 1 to filter to real handlers.
    /// </summary>
    [GeneratedRegex(@"\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:async\s+)?[\w<>,\s\.\?\[\]]+?\s+(?<name>[A-Z][\w]*)\s*\(")]
    private static partial Regex MethodDeclarationPattern();

    /// <summary>
    /// Matches a single parameter line whose type ends in <c>Request</c>
    /// (e.g. <c>BlobDeleteRequest request,</c> or <c>RevokePublicLinkRequest? request,</c>).
    /// </summary>
    [GeneratedRegex(@"(?<type>[A-Z][\w]*" + BodyDtoSuffix + @")\??\s+\w+\s*[,)]")]
    private static partial Regex RequestParamPattern();

    [GeneratedRegex(
        @"public\s+static\s+\S+\s+(Map\w+?)(?:<[^>]+>)?\s*\(\s*this\s+IEndpointRouteBuilder",
        RegexOptions.Multiline)]
    private static partial Regex PublicMapExtensionMethod();
}
