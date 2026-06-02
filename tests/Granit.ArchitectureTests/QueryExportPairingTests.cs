using Granit.ArchitectureTests.Abstractions.Rules;
using Granit.DataExchange.Export;
using Granit.QueryEngine;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces ADR-020: every QueryDefinition must have a paired ExportDefinition and vice versa.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class QueryExportPairingTests
{
    [Fact]
    public void Every_QueryDefinition_should_have_a_matching_ExportDefinition() =>
        QueryExportPairingRules.EveryQueryDefinitionShouldHaveExportDefinition(
            typeof(QueryExportPairingTests).Assembly,
            "Granit.*.dll",
            typeof(QueryDefinition<>),
            typeof(ExportDefinition<>),
            PairingExemptions.Infrastructure);

    [Fact]
    public void Every_ExportDefinition_should_have_a_matching_QueryDefinition() =>
        QueryExportPairingRules.EveryExportDefinitionShouldHaveQueryDefinition(
            typeof(QueryExportPairingTests).Assembly,
            "Granit.*.dll",
            typeof(QueryDefinition<>),
            typeof(ExportDefinition<>),
            PairingExemptions.Infrastructure);
}
