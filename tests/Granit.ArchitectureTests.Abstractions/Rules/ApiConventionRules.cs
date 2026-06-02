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

    [GeneratedRegex(@"(?:Task<IResult>|IResult)\s+\w+Async?\s*\(", RegexOptions.Multiline)]
    private static partial Regex BareIResultReturn();

    [GeneratedRegex(@"Results<((?:[^<>]|<[^<>]*>)+)>\s*>\s+\w+", RegexOptions.Multiline)]
    private static partial Regex ResultsUnionType();

    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch)\s*\(", RegexOptions.Multiline)]
    private static partial Regex EndpointRegistration();

    [GeneratedRegex(
        @"public\s+static\s+\S+\s+(Map\w+?)(?:<[^>]+>)?\s*\(\s*this\s+IEndpointRouteBuilder",
        RegexOptions.Multiline)]
    private static partial Regex PublicMapExtensionMethod();
}
