using System.Reflection;
using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates DTO naming conventions in endpoint packages:
/// no *Dto suffix, use *Request / *Response instead.
/// </summary>
public sealed class DtoConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void Endpoint_types_should_not_use_Dto_suffix()
    {
        // Auto-discover endpoint namespaces from loaded architecture graph
        string[] endpointNamespaces = [.. Architecture.Namespaces
            .Select(ns => ns.FullName)
            .Where(ns => ns.StartsWith("Granit.", StringComparison.Ordinal)
                && ns.EndsWith(".Endpoints", StringComparison.Ordinal))
            .Distinct()];

        NamingConventionRules.EndpointTypesShouldNotUseDtoSuffix(Architecture, endpointNamespaces);
    }

    [Fact]
    public void Nullable_request_parameters_must_have_a_default() =>
        RequestDtoNullableDefaultRules.NullableRequestParametersMustHaveDefault(
            LoadEndpointAssemblies(),
            RequestDtoNullableDefaultExemptions.RequiredButNullable);

    /// <summary>
    /// Loads the <c>Granit.*.Endpoints</c> assemblies from the test output directory so the rule can
    /// reflect over their request DTOs' constructor parameter defaults and nullability.
    /// </summary>
    private static IReadOnlyList<Assembly> LoadEndpointAssemblies()
    {
        string outputDir = Path.GetDirectoryName(typeof(DtoConventionTests).Assembly.Location)!;

        return [.. Directory.GetFiles(outputDir, "Granit.*.Endpoints.dll")
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Tests", StringComparison.Ordinal))
            .Select(LoadOrNull)
            .Where(a => a is not null)
            .Cast<Assembly>()];
    }

    private static Assembly? LoadOrNull(string path)
    {
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            return null;
        }
    }
}
