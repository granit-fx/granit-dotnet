using Granit.ArchitectureTests.Abstractions.Rules;
using Granit.QueryEngine;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces data-lookup coverage: every filterable foreign-key (<c>*Id</c>) column on a
/// <c>QueryDefinition&lt;T&gt;</c> must declare <c>.Lookup("&lt;source&gt;")</c> so the admin grid
/// renders a typeahead picker (and the same lookup powers the <c>@</c> mention picker). Logic lives
/// in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos reuse it.
/// </summary>
public sealed class QueryDefinitionLookupTests
{
    [Fact]
    public void Every_filterable_foreign_key_column_should_declare_a_lookup() =>
        QueryDefinitionLookupRules.EveryFilterableForeignKeyColumnShouldDeclareLookup(
            typeof(QueryDefinitionLookupTests).Assembly,
            "Granit.*.dll",
            typeof(QueryDefinition<>),
            LookupExemptions.Columns);
}
