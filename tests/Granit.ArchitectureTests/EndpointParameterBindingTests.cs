using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that all interface-typed parameters in minimal API endpoint handlers
/// are decorated with <c>[FromServices]</c> (or another explicit binding attribute).
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core's service inference checks <see cref="System.IServiceProvider"/> at routing
/// initialization time. If an interface type is not registered or the inference heuristic
/// fails, ASP.NET Core falls back to <c>[FromBody]</c> — which throws an
/// <see cref="System.InvalidOperationException"/> at startup on GET/DELETE endpoints
/// (which disallow body inference).
/// </para>
/// <para>
/// The convention in Granit is to always use <c>[FromServices]</c> explicitly on service
/// interface parameters, regardless of whether service inference would work.
/// </para>
/// </remarks>
/// <seealso cref="Granit.Analyzers.MinimalApiServiceParameterAnalyzer"/>
public sealed partial class EndpointParameterBindingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// ASP.NET Core interface types that bind from the HTTP request body or form data,
    /// not from the DI container — do not require <c>[FromServices]</c>.
    /// </summary>
    private static readonly HashSet<string> ExemptInterfaceTypes = new(StringComparer.Ordinal)
    {
        "IFormFile",
        "IFormFileCollection",
    };

    private static readonly HashSet<string> BindingAttributes = new(StringComparer.Ordinal)
    {
        "FromServices",
        "FromQuery",
        "FromRoute",
        "FromBody",
        "FromHeader",
        "FromForm",
        "AsParameters",
    };

    /// <summary>
    /// Interface-typed parameters in minimal API endpoint handler methods must be
    /// decorated with <c>[FromServices]</c> so that ASP.NET Core resolves them from DI
    /// instead of attempting to bind them from the request body.
    /// </summary>
    [Fact]
    public void Endpoint_service_parameters_must_have_FromServices()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string[] lines = File.ReadAllLines(csFile);
            CheckFile(csFile, lines, violations);
        }

        violations.ShouldBeEmpty(
            "Endpoint handler parameters of interface type must be decorated with [FromServices]. " +
            "ASP.NET Core may infer unbound interface parameters as [FromBody] and throw at startup. " +
            "Add [FromServices] before the parameter type." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static void CheckFile(string csFile, string[] lines, List<string> violations)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            // Skip lines that already carry an inline binding attribute
            if (HasBindingAttribute(trimmed))
            {
                continue;
            }

            // Detect a parameter whose type starts with an interface name (I[A-Z]...)
            Match m = InterfaceParamLine().Match(trimmed);
            if (!m.Success)
            {
                continue;
            }

            string typeName = m.Groups["type"].Value;
            if (ExemptInterfaceTypes.Contains(typeName))
            {
                continue;
            }

            // Accept [FromServices] (or other binding attr) on the immediately preceding
            // non-empty line (standalone attribute on its own line).
            bool prevLineHasAttr = false;
            for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
            {
                string prev = lines[j].TrimStart();
                if (string.IsNullOrWhiteSpace(prev))
                {
                    continue;
                }

                prevLineHasAttr = HasBindingAttribute(prev);
                break;
            }

            if (prevLineHasAttr)
            {
                continue;
            }

            // Only report violations inside endpoint handler methods.
            // Walk the paren tree backwards to find the containing method/lambda declaration
            // and verify it looks like a minimal API handler (static IResult-returning method
            // or a lambda passed to a Map* route registration call).
            if (!IsInsideEndpointHandlerParams(lines, i))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(RepoRoot, csFile);
            violations.Add($"  {relativePath}:{i + 1} — '{typeName}' needs [FromServices]");
        }
    }

    /// <summary>
    /// Walks backwards from <paramref name="paramLineIndex"/> through the paren tree
    /// to find the method or lambda declaration that contains this parameter, and returns
    /// <c>true</c> only if it looks like a minimal API endpoint handler.
    /// </summary>
    /// <remarks>
    /// Paren-depth tracking (scanning backwards, starting at depth 0):
    /// <list type="bullet">
    ///   <item><c>)</c> → depth++ (entering a nested paren context going outward)</item>
    ///   <item><c>(</c> → depth-- (exiting a paren context); stops when depth ≤ 0</item>
    /// </list>
    /// The first line where depth drops to ≤ 0 is the declaration line.
    /// </remarks>
    private static bool IsInsideEndpointHandlerParams(string[] lines, int paramLineIndex)
    {
        int depth = 0;
        for (int j = paramLineIndex; j >= Math.Max(0, paramLineIndex - 30); j--)
        {
            string line = lines[j];
            for (int k = line.Length - 1; k >= 0; k--)
            {
                char c = line[k];
                if (c == ')')
                {
                    depth++;
                }
                else if (c == '(')
                {
                    depth--;
                    if (depth <= 0)
                    {
                        return LooksLikeEndpointHandlerDeclaration(line);
                    }
                }
            }
        }

        return false;
    }

    private static bool LooksLikeEndpointHandlerDeclaration(string line)
    {
        // Lambda passed to a Map* route registration call
        if (MapMethodPattern().IsMatch(line))
        {
            return true;
        }

        // Must be a static method — constructors, instance methods, and class/record
        // primary constructors are excluded.
        if (!line.Contains("static ", StringComparison.Ordinal))
        {
            return false;
        }

        // Class/record primary constructors use 'static' on the type itself
        if (line.Contains(" class ", StringComparison.Ordinal) ||
            line.Contains(" record ", StringComparison.Ordinal))
        {
            return false;
        }

        // void or partial methods (e.g. [LoggerMessage] source-generated methods)
        if (line.Contains(" void ", StringComparison.Ordinal) ||
            line.Contains(" partial ", StringComparison.Ordinal))
        {
            return false;
        }

        // Return type must look like an IResult-returning endpoint handler
        return IResultReturnTypePattern().IsMatch(line);
    }

    private static bool HasBindingAttribute(string line) =>
        BindingAttributes.Any(attr =>
            line.Contains($"[{attr}]", StringComparison.Ordinal) ||
            line.Contains($"[{attr},", StringComparison.Ordinal) ||
            line.Contains($"[{attr} ", StringComparison.Ordinal));

    /// <summary>
    /// Matches a parameter line whose type is an interface (I[A-Z]...), optionally generic,
    /// followed by a parameter name, ending with <c>,</c> or <c>)</c>.
    /// </summary>
    [GeneratedRegex(@"^(?<type>I[A-Z]\w*)(?:<[^>]+>)?\s+\w+\s*[,)]")]
    private static partial Regex InterfaceParamLine();

    /// <summary>Matches a Map* route registration call.</summary>
    [GeneratedRegex(@"\bMap(?:Get|Post|Put|Delete|Patch|Methods?)\s*\(")]
    private static partial Regex MapMethodPattern();

    /// <summary>
    /// Matches return types characteristic of minimal API endpoint handlers:
    /// <c>IResult</c>, <c>Results&lt;…&gt;</c>, or <c>Task</c>/<c>ValueTask</c> wrapping
    /// an IResult-family type (<c>IResult</c>, <c>Results&lt;&gt;</c>, <c>Ok</c>,
    /// <c>Created</c>, <c>NoContent</c>, <c>Accepted</c>, <c>NotFound</c>,
    /// <c>BadRequest</c>, <c>Problem</c>).
    /// </summary>
    [GeneratedRegex(@"\b(?:IResult\b|Results<|(?:Task|ValueTask)<\s*(?:IResult\b|Results<|Ok\b|Created\b|NoContent\b|Accepted\b|NotFound\b|BadRequest\b|Problem))")]
    private static partial Regex IResultReturnTypePattern();

    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Only scan files whose name contains "Endpoint" — excludes DTOs, helpers,
            // and regular DI classes that happen to live inside an Endpoints module directory.
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
        string? dir = Path.GetDirectoryName(typeof(EndpointParameterBindingTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }
}
