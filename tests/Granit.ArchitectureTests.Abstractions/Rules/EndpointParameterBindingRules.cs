using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rule: interface-typed parameters in Minimal API endpoint handlers
/// must be decorated with <c>[FromServices]</c>.
/// </summary>
public static partial class EndpointParameterBindingRules
{
    private static readonly HashSet<string> ExemptInterfaceTypes = new(StringComparer.Ordinal)
    {
        "IFormFile",
        "IFormFileCollection",
    };

    private static readonly HashSet<string> BindingAttributes = new(StringComparer.Ordinal)
    {
        "FromServices", "FromQuery", "FromRoute", "FromBody",
        "FromHeader", "FromForm", "AsParameters",
    };

    /// <summary>
    /// Interface-typed parameters in Minimal API endpoint handler methods must be decorated
    /// with <c>[FromServices]</c> to prevent ASP.NET Core from binding them as request body.
    /// </summary>
    /// <param name="srcDir">Path to the repo's <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative path messages.</param>
    public static void EndpointServiceParametersMustHaveFromServices(
        string srcDir,
        string repoRoot)
    {
        List<string> violations = [];

        foreach (string csFile in GetEndpointSourceFiles(srcDir))
        {
            string[] lines = File.ReadAllLines(csFile);
            CheckFile(repoRoot, csFile, lines, violations);
        }

        violations.ShouldBeEmpty(
            "Endpoint handler parameters of interface type must be decorated with [FromServices]. " +
            "ASP.NET Core may infer unbound interface parameters as [FromBody] and throw at startup." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static void CheckFile(string repoRoot, string csFile, string[] lines, List<string> violations)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (HasBindingAttribute(trimmed))
            {
                continue;
            }

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

            if (!IsInsideEndpointHandlerParams(lines, i))
            {
                continue;
            }

            violations.Add($"  {Path.GetRelativePath(repoRoot, csFile)}:{i + 1} — '{typeName}' needs [FromServices]");
        }
    }

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
        if (MapMethodPattern().IsMatch(line))
        {
            return true;
        }

        if (!line.Contains("static ", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains(" class ", StringComparison.Ordinal) || line.Contains(" record ", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains(" void ", StringComparison.Ordinal) || line.Contains(" partial ", StringComparison.Ordinal))
        {
            return false;
        }

        return IResultReturnTypePattern().IsMatch(line);
    }

    private static bool HasBindingAttribute(string line) =>
        BindingAttributes.Any(attr =>
            line.Contains($"[{attr}]", StringComparison.Ordinal)
            || line.Contains($"[{attr},", StringComparison.Ordinal)
            || line.Contains($"[{attr} ", StringComparison.Ordinal));

    private static IEnumerable<string> GetEndpointSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            if (Path.GetFileNameWithoutExtension(csFile).Contains("Endpoint", StringComparison.Ordinal))
            {
                yield return csFile;
            }
        }
    }

    [GeneratedRegex(@"^(?<type>I[A-Z]\w*)(?:<[^>]+>)?\s+\w+\s*[,)]")]
    private static partial Regex InterfaceParamLine();

    [GeneratedRegex(@"\bMap(?:Get|Post|Put|Delete|Patch|Methods?)\s*\(")]
    private static partial Regex MapMethodPattern();

    [GeneratedRegex(@"\b(?:IResult\b|Results<|(?:Task|ValueTask)<\s*(?:IResult\b|Results<|Ok\b|Created\b|NoContent\b|Accepted\b|NotFound\b|BadRequest\b|Problem))")]
    private static partial Regex IResultReturnTypePattern();
}
