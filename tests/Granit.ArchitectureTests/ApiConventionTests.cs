using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates HTTP response conventions for Minimal API endpoints:
/// <list type="bullet">
/// <item>Endpoint handlers should use <c>Results&lt;...&gt;</c> union types, not bare <c>Task&lt;IResult&gt;</c></item>
/// <item>Union types with error paths should include <c>ProblemHttpResult</c></item>
/// </list>
/// </summary>
/// <seealso href="docs/framework/api/http-responses.md"/>
public sealed partial class ApiConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Known exemptions from the <c>Results&lt;...&gt;</c> union type requirement:
    /// <list type="bullet">
    /// <item><c>QueryEndpointHandler</c> — internal generic query infrastructure, returns dynamic shapes</item>
    /// <item><c>WebhookRedeliveryEndpoint</c> — returns polymorphic status codes based on redelivery result</item>
    /// <item><c>LocalizationEndpointRouteBuilderExtensions</c> — polymorphic returns (JSON resources + overrides), pending refactoring</item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> BareIResultExemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "QueryEndpointHandler",
        "WebhookRedeliveryEndpoint",
        "LocalizationEndpointRouteBuilderExtensions",
    };

    /// <summary>
    /// Endpoint handler methods returning <c>Task&lt;IResult&gt;</c> or bare <c>IResult</c>
    /// should use <c>Results&lt;...&gt;</c> union types instead, to provide compile-time type
    /// safety and automatic OpenAPI response documentation.
    /// </summary>
    [Fact]
    public void Endpoint_handlers_should_use_typed_results_union()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            string fileName = Path.GetFileNameWithoutExtension(csFile);

            if (BareIResultExemptions.Contains(fileName))
            {
                continue;
            }

            foreach (Match match in BareIResultReturn().Matches(content))
            {
                string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                string methodSignature = match.Value.Trim();
                violations.Add($"{relativePath}:{lineNumber} ({methodSignature})");
            }
        }

        violations.ShouldBeEmpty(
            "Endpoint handlers should return Results<...> union types instead of Task<IResult> or IResult. " +
            "Union types provide compile-time type safety and automatic OpenAPI response documentation. " +
            "See docs/framework/api/http-responses.md. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// <c>Results&lt;...&gt;</c> union types in endpoint handler methods that include
    /// error-related types (<c>NotFound</c>, <c>ValidationProblem</c>, <c>UnauthorizedHttpResult</c>)
    /// should also include <c>ProblemHttpResult</c> for unexpected error paths, unless the
    /// union only contains simple success + not-found combinations.
    /// </summary>
    /// <remarks>
    /// Exempted patterns (no <c>ProblemHttpResult</c> required):
    /// <list type="bullet">
    /// <item><c>Results&lt;Created, Ok&gt;</c> — upsert pattern, no error path</item>
    /// <item><c>Results&lt;NoContent, NotFound&gt;</c> — simple delete/update, errors via middleware</item>
    /// <item><c>Results&lt;Ok, NotFound&gt;</c> — simple read, errors via middleware</item>
    /// <item><c>Results&lt;Accepted, NotFound&gt;</c> — async trigger, errors via middleware</item>
    /// <item><c>Results&lt;NoContent, ValidationProblem&gt;</c> — validation only, no business logic errors</item>
    /// </list>
    /// These simple two-type unions rely on <c>GranitExceptionHandler</c> middleware for unexpected errors,
    /// which is an acceptable pattern.
    /// </remarks>
    [Fact]
    public void Complex_results_unions_should_include_ProblemHttpResult()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);

            foreach (Match match in ResultsUnionType().Matches(content))
            {
                string unionArgs = match.Groups[1].Value;

                // Already includes ProblemHttpResult — compliant
                if (unionArgs.Contains("ProblemHttpResult", StringComparison.Ordinal))
                {
                    continue;
                }

                // Count type arguments (split on comma, accounting for generic nesting)
                int typeCount = CountTypeArguments(unionArgs);

                // Two-type unions are exempt — simple patterns handled by middleware
                if (typeCount <= 2)
                {
                    continue;
                }

                // Unions composed only of success types + NotFound/NoContent/ValidationProblem
                // are exempt — no custom business error path requiring ProblemHttpResult
                if (AllTypesAreSimple(unionArgs))
                {
                    continue;
                }

                // Three+ type unions with business logic types should include ProblemHttpResult
                string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber} (Results<{unionArgs}>)");
            }
        }

        violations.ShouldBeEmpty(
            "Results<...> union types with 3+ type arguments should include ProblemHttpResult " +
            "to document error responses in OpenAPI. Two-type unions are exempt (simple patterns " +
            "rely on GranitExceptionHandler middleware). See docs/framework/api/http-responses.md. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Every endpoint registration (<c>.MapGet()</c>, <c>.MapPost()</c>, etc.) in
    /// <c>*.Endpoints</c> packages must declare all mandatory OpenAPI metadata:
    /// <c>.WithName()</c>, <c>.WithSummary()</c>, <c>.WithDescription()</c>,
    /// and at least one <c>.Produces()</c> variant.
    /// </summary>
    [Fact]
    public void Endpoint_registrations_should_have_complete_OpenAPI_metadata()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetEndpointPackageSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);

            foreach (Match match in EndpointRegistration().Matches(content))
            {
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                string chain = ExtractFluentChain(content, match.Index);

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
                    string verb = match.Groups[1].Value;
                    violations.Add(
                        $"{relativePath}:{lineNumber} (.Map{verb}) missing: {string.Join(", ", missing)}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every endpoint registration must declare WithName, WithSummary, WithDescription, " +
            "and at least one Produces variant for OpenAPI documentation. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Extracts the fluent method chain starting at <paramref name="startIndex"/>
    /// until the terminating semicolon, tracking brace/parenthesis depth to skip
    /// nested lambdas.
    /// </summary>
    private static string ExtractFluentChain(string content, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < content.Length; i++)
        {
            char c = content[i];
            switch (c)
            {
                case '(' or '{':
                    depth++;
                    break;
                case ')' or '}':
                    depth--;
                    break;
                case ';' when depth <= 0:
                    return content[startIndex..i];
            }
        }

        return content[startIndex..];
    }

    /// <summary>
    /// Enumerates C# source files in <c>Granit.*.Endpoints</c> packages only.
    /// Used by the OpenAPI metadata test to focus on user-facing API endpoints
    /// (excludes infrastructure endpoints in Core, Validation, Features, etc.).
    /// </summary>
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

            if (!moduleName.EndsWith(".Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    /// <summary>
    /// Enumerates all C# source files in endpoint-related directories under <paramref name="srcDir"/>.
    /// </summary>
    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Only scan endpoint-related packages
            string relativePath = Path.GetRelativePath(srcDir, csFile);
            string moduleName = relativePath.Split(Path.DirectorySeparatorChar)[0];

            if (!moduleName.Contains("Endpoints", StringComparison.Ordinal)
                && !csFile.Contains(Path.DirectorySeparatorChar + "Endpoints" + Path.DirectorySeparatorChar)
                && !csFile.Contains("Endpoint", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    /// <summary>
    /// Counts comma-separated type arguments, correctly handling nested generics like
    /// <c>Ok&lt;T&gt;</c> by tracking angle bracket depth.
    /// </summary>
    private static int CountTypeArguments(string typeArgs)
    {
        int count = 1;
        int depth = 0;

        foreach (char c in typeArgs)
        {
            switch (c)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    count++;
                    break;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns <c>true</c> when all type arguments are "simple" types that don't
    /// represent custom business error paths requiring <c>ProblemHttpResult</c>.
    /// Simple types: <c>Ok</c>, <c>Ok&lt;T&gt;</c>, <c>Created</c>, <c>Created&lt;T&gt;</c>,
    /// <c>Accepted</c>, <c>Accepted&lt;T&gt;</c>, <c>NoContent</c>, <c>NotFound</c>,
    /// <c>NotFound&lt;T&gt;</c>, <c>ValidationProblem</c>, <c>FileStreamHttpResult</c>,
    /// <c>UnauthorizedHttpResult</c>.
    /// </summary>
    private static bool AllTypesAreSimple(string typeArgs)
    {
        // Split at top-level commas (depth 0), then extract the base type name
        List<string> types = [];
        int depth = 0;
        int start = 0;

        for (int i = 0; i <= typeArgs.Length; i++)
        {
            if (i == typeArgs.Length || (typeArgs[i] == ',' && depth == 0))
            {
                string part = typeArgs[start..i].Trim();
                // Extract base type name (before any <T>)
                int angleBracket = part.IndexOf('<');
                string baseName = angleBracket >= 0 ? part[..angleBracket].Trim() : part;
                types.Add(baseName);
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

        HashSet<string> simpleTypes = new(StringComparer.Ordinal)
        {
            "Ok", "Created", "Accepted", "NoContent",
            "NotFound", "ValidationProblem", "FileStreamHttpResult",
            "UnauthorizedHttpResult", "ForbidResult",
        };

        return types.All(t => simpleTypes.Contains(t));
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ApiConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches method signatures returning <c>Task&lt;IResult&gt;</c> or bare <c>IResult</c>
    /// in endpoint handler methods. Captures the full return type + method name.
    /// </summary>
    [GeneratedRegex(@"(?:Task<IResult>|IResult)\s+\w+Async?\s*\(", RegexOptions.Multiline)]
    private static partial Regex BareIResultReturn();

    /// <summary>
    /// Matches <c>Results&lt;...&gt;</c> type declarations in method return types.
    /// Captures the inner type arguments (group 1).
    /// Uses a non-greedy match that handles one level of nested generics.
    /// </summary>
    [GeneratedRegex(@"Results<((?:[^<>]|<[^<>]*>)+)>\s*>\s+\w+", RegexOptions.Multiline)]
    private static partial Regex ResultsUnionType();

    /// <summary>
    /// Matches endpoint registration calls: <c>.MapGet(</c>, <c>.MapPost(</c>,
    /// <c>.MapPut(</c>, <c>.MapDelete(</c>, <c>.MapPatch(</c>.
    /// Captures the HTTP verb (group 1).
    /// </summary>
    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch)\s*\(", RegexOptions.Multiline)]
    private static partial Regex EndpointRegistration();
}
