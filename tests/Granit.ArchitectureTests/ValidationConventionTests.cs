using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates validation conventions for Granit endpoint packages:
/// <list type="bullet">
/// <item>Every <c>*Request</c> type in <c>.Endpoints</c> assemblies must have a registered <c>IValidator&lt;T&gt;</c></item>
/// <item>Top-level route groups must use <c>MapGranitGroup</c> instead of <c>MapGroup</c></item>
/// </list>
/// </summary>
public sealed partial class ValidationConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Known exemptions from the validator requirement.
    /// These <c>*Request</c> types intentionally have no validator because they contain
    /// no user-supplied fields requiring validation (e.g. query/filter DTOs with only
    /// optional parameters, or types validated entirely in the handler).
    /// </summary>
    private static readonly HashSet<string> ValidatorExemptions = new(StringComparer.Ordinal)
    {
        // Query/list requests with only optional filter params
        "ApiKeyListRequest",
        "IdentityUserCacheListRequest",
        // Request with only a value that is validated in handler via IFeatureDefinitionStore
        "TemplatePreviewRequest",
        // Query-string binding wrapper — no body to validate
        "BindableQueryRequest",
        // TODO: needs a validator (pre-existing gap, tracked separately)
        "MobilePushTokenRegisterRequest",
    };

    // -------------------------------------------------------------------------
    // Test A — Reflection: all *Request types must have a validator
    // -------------------------------------------------------------------------

    /// <summary>
    /// Every <c>*Request</c> type declared in a <c>Granit.*.Endpoints</c> assembly
    /// must have a corresponding <c>IValidator&lt;T&gt;</c> implementation in the same assembly.
    /// </summary>
    [Fact]
    public void Request_types_in_Endpoints_should_have_validators()
    {
        string outputDir = Path.GetDirectoryName(typeof(ValidationConventionTests).Assembly.Location)!;

        Assembly[] endpointAssemblies = Directory.GetFiles(outputDir, "Granit.*.Endpoints.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests", StringComparison.Ordinal))
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in endpointAssemblies)
        {
            Type[] allTypes;
            try { allTypes = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t is not null).ToArray()!; }

            Type[] requestTypes = allTypes
                .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal)
                    && t.IsPublic
                    && !t.IsAbstract
                    && !t.IsInterface
                    && !ValidatorExemptions.Contains(t.Name))
                .ToArray();

            foreach (Type requestType in requestTypes)
            {
                Type validatorInterface = typeof(IValidator<>).MakeGenericType(requestType);

                bool hasValidator = allTypes.Any(t =>
                    !t.IsAbstract
                    && !t.IsInterface
                    && validatorInterface.IsAssignableFrom(t));

                if (!hasValidator)
                {
                    violations.Add($"{assembly.GetName().Name}: {requestType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every *Request type in .Endpoints assemblies must have a corresponding " +
            "IValidator<T> implementation. Add a GranitValidator<T> in the Validators/ folder " +
            "or add the type to ValidatorExemptions if validation is intentionally skipped. " +
            $"Missing validators: {string.Join("; ", violations)}");
    }

    // -------------------------------------------------------------------------
    // Test B — Source scanning: top-level route groups must use MapGranitGroup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Top-level route group creation in <c>*.Endpoints</c> packages must use
    /// <c>endpoints.MapGranitGroup()</c> instead of <c>endpoints.MapGroup()</c>
    /// to ensure automatic FluentValidation is applied.
    /// </summary>
    [Fact]
    public void Top_level_route_groups_should_use_MapGranitGroup()
    {
        string srcDir = Path.Combine(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetEndpointRegistrationFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);

            foreach (Match match in TopLevelMapGroupCall().Matches(content))
            {
                string relativePath = Path.GetRelativePath(RepoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "Top-level route groups in .Endpoints packages should use " +
            "endpoints.MapGranitGroup() instead of endpoints.MapGroup() " +
            "to enable automatic FluentValidation. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enumerates C# files in endpoint packages that contain route group registrations.
    /// </summary>
    private static IEnumerable<string> GetEndpointRegistrationFiles(string srcDir)
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

            if (!moduleName.Contains("Endpoints", StringComparison.Ordinal))
            {
                continue;
            }

            // Only scan files that define route groups
            if (!csFile.Contains("EndpointRouteBuilder", StringComparison.Ordinal)
                && !csFile.Contains("Endpoints" + Path.DirectorySeparatorChar + "Endpoints", StringComparison.Ordinal)
                && !Path.GetFileName(csFile).EndsWith("Endpoints.cs", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ValidationConventionTests).Assembly.Location);
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
    /// Matches <c>endpoints.MapGroup(</c> calls — top-level route group creation
    /// on an <c>IEndpointRouteBuilder</c> variable. Does NOT match sub-group calls
    /// like <c>group.MapGroup(</c> or <c>adminGroup.MapGroup(</c>.
    /// </summary>
    [GeneratedRegex(@"endpoints\s*\.\s*MapGroup\s*\(", RegexOptions.Multiline)]
    private static partial Regex TopLevelMapGroupCall();
}
