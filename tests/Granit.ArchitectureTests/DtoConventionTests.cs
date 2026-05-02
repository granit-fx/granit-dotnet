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
}
