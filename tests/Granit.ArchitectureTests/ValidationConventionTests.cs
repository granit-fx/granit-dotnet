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
/// <item>Validators must not use hardcoded <c>.WithMessage("...")</c> strings</item>
/// </list>
/// </summary>
public sealed partial class ValidationConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Property types that have no meaningful FluentValidation rules.
    /// A <c>*Request</c> whose public properties are ALL of these types is auto-exempted.
    /// </summary>
    private static readonly HashSet<Type> ValidationFreeTypes = [typeof(bool)];

    /// <summary>
    /// Known exemptions from the validator requirement.
    /// These <c>*Request</c> types intentionally have no validator because they contain
    /// no user-supplied fields requiring validation (e.g. query/filter DTOs with only
    /// optional parameters, or types validated entirely in the handler).
    /// </summary>
    /// <remarks>
    /// Before adding an exemption here, check whether the type can be auto-exempted:
    /// <list type="bullet">
    /// <item>Types with only <see cref="ValidationFreeTypes"/> properties are auto-skipped</item>
    /// <item>Types with a static <c>BindAsync</c> method (custom binding wrappers) are auto-skipped</item>
    /// </list>
    /// </remarks>
    private static readonly HashSet<string> ValidatorExemptions = new(StringComparer.Ordinal)
    {
        // Request with only optional fields validated in handler via IFeatureDefinitionStore
        "TemplatePreviewRequest",
        // Nested sub-type validated via ChildRules in AIChatRequestValidator — never sent as direct body
        "AIChatMessageRequest",
        // Query-string DTO with only optional nullable filters — pagination clamped in handler
        "AuditLogQueryRequest",
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
                    && !ValidatorExemptions.Contains(t.Name)
                    && !IsCustomBindingType(t)
                    && !HasOnlyValidationFreeProperties(t))
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
        string srcDir = Path.Join(RepoRoot, "src");

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
    // Test C — Source scanning: no hardcoded .WithMessage() in validators
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validator classes must not use <c>.WithMessage("hardcoded string")</c>.
    /// Built-in validators are auto-converted to error codes by <c>GranitErrorCodeLanguageManager</c>.
    /// Custom <c>.Must()</c> validators must use <c>.WithErrorCodeAndMessage("Granit:Validation:XxxCode")</c>.
    /// </summary>
    [Fact]
    public void Validators_should_not_use_hardcoded_WithMessage()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        List<string> violations = [];

        foreach (string csFile in GetValidatorFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);

            foreach (Match match in HardcodedWithMessage().Matches(content))
            {
                string message = match.Groups[1].Value;

                // Error codes starting with "Granit:" are acceptable (used with WithMessage
                // as a transitional pattern before WithErrorCodeAndMessage)
                if (message.StartsWith("Granit:", StringComparison.Ordinal))
                {
                    continue;
                }

                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber} .WithMessage(\"{message}\")");
            }
        }

        violations.ShouldBeEmpty(
            "Validators must not use hardcoded .WithMessage(\"...\") strings. " +
            "Use .WithErrorCodeAndMessage(\"Granit:Validation:XxxCode\") and add the key " +
            "to the localization JSON files in src/Granit.Validation/Localization/Validation/. " +
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

    /// <summary>
    /// Enumerates all <c>*Validator.cs</c> files in <c>*.Endpoints</c> packages.
    /// </summary>
    private static IEnumerable<string> GetValidatorFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*Validator.cs", SearchOption.AllDirectories))
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
    /// Returns <c>true</c> when the type declares a static <c>BindAsync</c> method,
    /// which means it is a custom binding wrapper (query-string binding) — not a JSON body
    /// and therefore not a candidate for FluentValidation.
    /// </summary>
    private static bool IsCustomBindingType(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Any(m => m.Name == "BindAsync");

    /// <summary>
    /// Returns <c>true</c> when every public instance property of the type is a
    /// <see cref="ValidationFreeTypes">validation-free type</see> (e.g. <c>bool</c>).
    /// Such types have no meaningful FluentValidation rules and are auto-exempted.
    /// </summary>
    private static bool HasOnlyValidationFreeProperties(Type type)
    {
        PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return props.Length > 0 && props.All(p => ValidationFreeTypes.Contains(p.PropertyType));
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(ValidationConventionTests).Assembly.Location);
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

    /// <summary>
    /// Matches <c>endpoints.MapGroup(</c> calls — top-level route group creation
    /// on an <c>IEndpointRouteBuilder</c> variable. Does NOT match sub-group calls
    /// like <c>group.MapGroup(</c> or <c>adminGroup.MapGroup(</c>.
    /// </summary>
    [GeneratedRegex(@"endpoints\s*\.\s*MapGroup\s*\(", RegexOptions.Multiline)]
    private static partial Regex TopLevelMapGroupCall();

    /// <summary>
    /// Matches <c>.WithMessage("literal string")</c> calls in validator source code.
    /// Captures the string value (group 1).
    /// </summary>
    [GeneratedRegex(@"\.WithMessage\(\s*""([^""]+)""\s*\)", RegexOptions.Multiline)]
    private static partial Regex HardcodedWithMessage();
}
