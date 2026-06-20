using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests.Abstractions.Tests;

/// <summary>
/// Behavioural tests for <see cref="QueryDefinitionLookupRules"/>: a filterable <c>*Id</c> column
/// without a lookup fails, while a wired column, an exempt column, and non-foreign-key columns pass.
/// Uses synthetic definitions so the rule is validated without a dependency on
/// <c>Granit.QueryEngine</c>.
/// </summary>
public sealed class QueryDefinitionLookupRulesTests
{
    private static readonly Type OpenBase = typeof(FakeQueryDefinition<>);

    [Fact]
    public void Filterable_foreign_key_without_lookup_fails()
    {
        Should.Throw<ShouldAssertException>(() =>
            QueryDefinitionLookupRules.EveryFilterableForeignKeyColumnShouldDeclareLookup(
                [typeof(UnwiredDefinition)], OpenBase));
    }

    [Fact]
    public void Filterable_foreign_key_with_lookup_passes()
    {
        Should.NotThrow(() =>
            QueryDefinitionLookupRules.EveryFilterableForeignKeyColumnShouldDeclareLookup(
                [typeof(WiredDefinition)], OpenBase));
    }

    [Fact]
    public void Unwired_foreign_key_passes_when_exempted()
    {
        HashSet<string> exemptions = new(StringComparer.Ordinal)
        {
            $"{typeof(UnwiredEntity).FullName}.PartyId",
        };

        Should.NotThrow(() =>
            QueryDefinitionLookupRules.EveryFilterableForeignKeyColumnShouldDeclareLookup(
                [typeof(UnwiredDefinition)], OpenBase, exemptions));
    }

    [Fact]
    public void Non_foreign_key_and_non_filterable_columns_are_ignored()
    {
        // Name (not an Id), Id (the primary key), and a non-filterable PlanId must all pass.
        Should.NotThrow(() =>
            QueryDefinitionLookupRules.EveryFilterableForeignKeyColumnShouldDeclareLookup(
                [typeof(EdgeCaseDefinition)], OpenBase));
    }

    // ── Synthetic fixtures ───────────────────────────────────────────────────────────────────────

    private sealed class UnwiredEntity;

    private sealed class WiredEntity;

    private sealed class EdgeEntity;

    private sealed record FakeColumn(string PropertyName, bool IsFilterable, object? Lookup);

    private abstract class FakeQueryDefinition<TEntity>
    {
        public abstract IReadOnlyList<FakeColumn> GetColumns();
    }

    private sealed class UnwiredDefinition : FakeQueryDefinition<UnwiredEntity>
    {
        public override IReadOnlyList<FakeColumn> GetColumns() =>
            [new FakeColumn("PartyId", IsFilterable: true, Lookup: null)];
    }

    private sealed class WiredDefinition : FakeQueryDefinition<WiredEntity>
    {
        public override IReadOnlyList<FakeColumn> GetColumns() =>
            [new FakeColumn("PartyId", IsFilterable: true, Lookup: new object())];
    }

    private sealed class EdgeCaseDefinition : FakeQueryDefinition<EdgeEntity>
    {
        public override IReadOnlyList<FakeColumn> GetColumns() =>
        [
            new FakeColumn("Name", IsFilterable: true, Lookup: null),
            new FakeColumn("Id", IsFilterable: true, Lookup: null),
            new FakeColumn("PlanId", IsFilterable: false, Lookup: null),
        ];
    }
}
