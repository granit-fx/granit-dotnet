using Granit.ArchitectureTests.Abstractions.Rules;
using Granit.QueryEngine;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces query-catalog completeness: every concrete <c>QueryDefinition&lt;T&gt;</c> that cannot use
/// the <c>new()</c>-constrained <c>AddQueryDefinition&lt;TEntity, TDefinition&gt;()</c> helper (no public
/// parameterless constructor) is registered by hand and must be listed in
/// <see cref="QueryDefinitionCatalogExemptions"/> — the review gate that keeps the manual
/// <c>IQueryDefinitionDescriptor</c> binding from being dropped, which would silently remove the query
/// from <c>GET /catalog</c> and the dashboard-widget query picker. Logic lives in
/// <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos reuse it.
/// </summary>
public sealed class QueryDefinitionCatalogTests
{
    [Fact]
    public void Every_bespoke_query_definition_should_be_exempt() =>
        QueryDefinitionCatalogRules.EveryBespokeDefinitionIsExempt(
            typeof(QueryDefinitionCatalogTests).Assembly,
            "Granit.*.dll",
            typeof(QueryDefinition<>),
            QueryDefinitionCatalogExemptions.Definitions);
}
