using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable validation convention rules:
/// every <c>*Request</c> type in endpoint assemblies must have an <c>IValidator&lt;T&gt;</c>;
/// top-level route groups must use <c>MapGranitGroup</c>.
/// </summary>
public static partial class ValidationConventionRules
{
    /// <summary>
    /// Every public, concrete <c>*Request</c> type in assemblies matching
    /// <paramref name="assemblyGlob"/> must have a corresponding
    /// <c>IValidator&lt;T&gt;</c> implementation in the same assembly.
    /// </summary>
    /// <param name="callerAssembly">The test assembly — used to locate the output directory.</param>
    /// <param name="assemblyGlob">Glob for endpoint assemblies (e.g. <c>"*.Endpoints.dll"</c> or <c>"Showcase.Modules.*.dll"</c>).</param>
    /// <param name="exemptions">Short type names exempt from the validator requirement.</param>
    public static void RequestTypesShouldHaveValidators(
        Assembly callerAssembly,
        string assemblyGlob = "*.Endpoints.dll",
        IReadOnlySet<string>? exemptions = null)
    {
        exemptions ??= new HashSet<string>(StringComparer.Ordinal);

        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;

        Assembly[] endpointAssemblies = Directory.GetFiles(outputDir, assemblyGlob)
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests", StringComparison.Ordinal))
            .Select(TryLoadAssembly)
            .Where(a => a is not null)
            .ToArray()!;

        List<string> violations = [];

        foreach (Assembly assembly in endpointAssemblies)
        {
            Type[] allTypes;
            try { allTypes = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(t => t is not null).ToArray()!; }

            Type[] requestTypes = [.. allTypes
                .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal)
                    && t.IsPublic && !t.IsAbstract && !t.IsInterface
                    && !exemptions.Contains(t.Name)
                    && !IsCustomBindingType(t)
                    && !HasOnlyBoolProperties(t))];

            foreach (Type requestType in requestTypes)
            {
                Type validatorInterface = typeof(IValidator<>).MakeGenericType(requestType);

                bool hasValidator = allTypes.Any(t =>
                    !t.IsAbstract && !t.IsInterface && validatorInterface.IsAssignableFrom(t));

                if (!hasValidator)
                {
                    violations.Add($"{assembly.GetName().Name}: {requestType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Every *Request type in endpoint assemblies must have a corresponding IValidator<T>. " +
            "Add a GranitValidator<T> in the Validators/ folder, or add the type to the exemptions " +
            "list if validation is intentionally skipped. " +
            $"Missing validators: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Top-level route group creation in endpoint packages must use
    /// <c>endpoints.MapGranitGroup()</c> instead of <c>endpoints.MapGroup()</c>
    /// to ensure automatic FluentValidation is applied.
    /// </summary>
    /// <param name="srcDir">Path to the repo's <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root — used to compute relative paths in violation messages.</param>
    /// <param name="additionalExemptFiles">Additional filenames (without extension) to skip.</param>
    public static void EndpointsShouldUseMapGranitGroup(
        string srcDir,
        string repoRoot,
        params string[] additionalExemptFiles)
    {
        HashSet<string> exemptFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            // Defines MapGranitGroup itself — legitimately calls MapGroup internally
            "GranitEndpointRouteBuilderExtensions",
        };
        foreach (string f in additionalExemptFiles)
        {
            exemptFiles.Add(f);
        }

        List<string> violations = [];

        foreach (string csFile in GetEndpointRegistrationFiles(srcDir))
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            if (exemptFiles.Contains(fileName))
            {
                continue;
            }

            string content = File.ReadAllText(csFile);

            foreach (Match match in TopLevelMapGroupCall().Matches(content))
            {
                string relativePath = Path.GetRelativePath(repoRoot, csFile);
                int lineNumber = content[..match.Index].Count(c => c == '\n') + 1;
                violations.Add($"{relativePath}:{lineNumber}");
            }
        }

        violations.ShouldBeEmpty(
            "Top-level route groups must use endpoints.MapGranitGroup() instead of " +
            "endpoints.MapGroup() to enable automatic FluentValidation. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static IEnumerable<string> GetEndpointRegistrationFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInBuildOutput(csFile))
            {
                continue;
            }

            if (!csFile.Contains("EndpointRouteBuilder", StringComparison.Ordinal)
                && !Path.GetFileName(csFile).EndsWith("Endpoints.cs", StringComparison.Ordinal))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static bool IsCustomBindingType(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Any(m => m.Name == "BindAsync");

    private static bool HasOnlyBoolProperties(Type type)
    {
        PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return props.Length > 0 && props.All(p => p.PropertyType == typeof(bool));
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
    }

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    [GeneratedRegex(@"endpoints\s*\.\s*MapGroup\s*\(", RegexOptions.Multiline)]
    private static partial Regex TopLevelMapGroupCall();
}
