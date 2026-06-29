using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Locks the Granit validation-vs-domain HTTP status-code contract so that the
/// runtime behaviour and the generated OpenAPI document agree:
/// <list type="bullet">
///   <item>
///     Field/body validation failures are <c>422 Unprocessable Entity</c>, never
///     <c>400 Bad Request</c>. <c>400</c> stays reserved for malformed requests and
///     domain errors carrying an error code (<c>BusinessException</c> / <c>IHasErrorCode</c>).
///   </item>
///   <item>
///     Every <c>.ProducesValidationProblem(...)</c> OpenAPI annotation must declare
///     <c>422</c> explicitly. A bare <c>.ProducesValidationProblem()</c> defaults to
///     <c>400</c> in the document while the runtime filter returns <c>422</c> — the
///     drift that shipped a framework-wide <c>400</c> into the front-end OpenAPI
///     snapshots even though no validation failure ever returns <c>400</c>.
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// Runtime side is centralised in two files this test pins:
/// <see cref="Granit.Validation"/>'s <c>FluentValidationAutoEndpointFilter</c> (the
/// auto endpoint filter) and <c>DefaultExceptionStatusCodeMapper</c> in
/// <c>Granit.Http.ExceptionHandling</c>. The OpenAPI side is the per-endpoint
/// <c>.ProducesValidationProblem(...)</c> metadata, scanned across all of <c>src/</c>.
/// </remarks>
public sealed partial class ValidationStatusCodeConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string ValidationStatus = "Status422UnprocessableEntity";

    [Fact]
    public void ProducesValidationProblem_metadata_must_target_422_not_400()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetSourceFiles(srcDir))
        {
            string[] lines = File.ReadAllLines(csFile);
            for (int i = 0; i < lines.Length; i++)
            {
                Match call = ProducesValidationProblemPattern().Match(lines[i]);
                if (!call.Success)
                {
                    continue;
                }

                string args = call.Groups["args"].Value;
                if (args.Contains(ValidationStatus, StringComparison.Ordinal)
                    || args.Contains("422", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(
                    $"  {Path.GetRelativePath(RepoRoot, csFile)}:{i + 1} — " +
                    ".ProducesValidationProblem() defaults to 400; declare " +
                    ".ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).");
            }
        }

        violations.ShouldBeEmpty(
            "Every .ProducesValidationProblem(...) must target 422 to match the runtime " +
            "FluentValidationAutoEndpointFilter, otherwise the generated OpenAPI document " +
            "advertises a 400 that no validation failure ever returns." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FluentValidation_filter_returns_422_for_body_validation_failures()
    {
        string filter = ReadCanonical("FluentValidationAutoEndpointFilter.cs");

        filter.ShouldContain(
            ValidationStatus,
            customMessage: "The auto endpoint filter must return 422 for body validation failures.");
        filter.ShouldNotContain(
            "Status400BadRequest",
            customMessage: "Body validation failures must never return 400 (reserved for domain errors).");
    }

    [Theory]
    [InlineData("ValidationException")]
    [InlineData("IHasValidationErrors")]
    [InlineData("BusinessRuleViolationException")]
    public void Validation_exceptions_map_to_422_in_the_central_mapper(string exceptionType)
    {
        string mapper = ReadCanonical("DefaultExceptionStatusCodeMapper.cs");

        // The switch arm for each validation-shaped exception must resolve to 422,
        // guarding against an accidental downgrade to 400.
        Match arm = Regex.Match(
            mapper,
            $@"{Regex.Escape(exceptionType)}\s*=>\s*StatusCodes\.(?<status>Status\d+\w+)");

        arm.Success.ShouldBeTrue(
            $"'{exceptionType}' must have an explicit status-code mapping in DefaultExceptionStatusCodeMapper.");
        arm.Groups["status"].Value.ShouldBe(
            ValidationStatus,
            customMessage: $"'{exceptionType}' must map to 422, not {arm.Groups["status"].Value}.");
    }

    private static string ReadCanonical(string fileName)
    {
        string srcDir = Path.Join(RepoRoot, "src");
        string? match = Directory
            .EnumerateFiles(srcDir, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(p => !IsBuildArtifact(p));

        match.ShouldNotBeNull(
            $"Could not locate canonical file '{fileName}' under src/. " +
            "If it was renamed or moved, update this convention test to point at its new home.");

        return File.ReadAllText(match);
    }

    /// <summary>
    /// Matches a single <c>.ProducesValidationProblem(...)</c> invocation and captures
    /// its argument list (empty for the bare, 400-defaulting form). The metadata call
    /// is always written on one line in the Granit codebase.
    /// </summary>
    [GeneratedRegex(@"\.ProducesValidationProblem\s*\(\s*(?<args>[^)]*)\)")]
    private static partial Regex ProducesValidationProblemPattern();

    private static IEnumerable<string> GetSourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsBuildArtifact(csFile))
            {
                yield return csFile;
            }
        }
    }

    private static bool IsBuildArtifact(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ValidationStatusCodeConventionTests).Assembly.Location);
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
