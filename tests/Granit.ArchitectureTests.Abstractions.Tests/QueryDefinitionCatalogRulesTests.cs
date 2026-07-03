using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests.Abstractions.Tests;

/// <summary>
/// Behavioural tests for <see cref="QueryDefinitionCatalogRules"/>: a concrete definition without a
/// public parameterless constructor (a hand-registered "bespoke" definition) fails unless exempt,
/// while a definition that can use the <c>new()</c>-constrained registration helper passes. Uses
/// synthetic definitions so the rule is validated without a dependency on <c>Granit.QueryEngine</c>.
/// </summary>
public sealed class QueryDefinitionCatalogRulesTests
{
    private static readonly Type OpenBase = typeof(FakeQueryDefinition<>);

    [Fact]
    public void Bespoke_definition_without_parameterless_constructor_fails()
    {
        Should.Throw<ShouldAssertException>(() =>
            QueryDefinitionCatalogRules.EveryBespokeDefinitionIsExempt(
                [typeof(BespokeDefinition)], OpenBase));
    }

    [Fact]
    public void Bespoke_definition_passes_when_exempted()
    {
        HashSet<string> exemptions = new(StringComparer.Ordinal)
        {
            typeof(BespokeDefinition).FullName!,
        };

        Should.NotThrow(() =>
            QueryDefinitionCatalogRules.EveryBespokeDefinitionIsExempt(
                [typeof(BespokeDefinition)], OpenBase, exemptions));
    }

    [Fact]
    public void Definition_with_public_parameterless_constructor_passes()
    {
        // Can flow through AddQueryDefinition<TEntity, TDefinition>() (new()-constrained), which binds
        // IQueryDefinitionDescriptor for free — so it is not bespoke and needs no exemption.
        Should.NotThrow(() =>
            QueryDefinitionCatalogRules.EveryBespokeDefinitionIsExempt(
                [typeof(StandardDefinition)], OpenBase));
    }

    [Fact]
    public void Abstract_and_open_generic_definitions_are_ignored()
    {
        // The abstract base and the open generic definition are not concrete closed types, so the
        // rule skips them even though the open generic lacks a usable parameterless constructor.
        Should.NotThrow(() =>
            QueryDefinitionCatalogRules.EveryBespokeDefinitionIsExempt(
                [OpenBase, typeof(OpenGenericDefinition<>)], OpenBase));
    }

    // ── Synthetic fixtures ───────────────────────────────────────────────────────────────────────

    private sealed class FakeEntity;

    private abstract class FakeQueryDefinition<TEntity>;

    private sealed class StandardDefinition : FakeQueryDefinition<FakeEntity>;

    private sealed class OpenGenericDefinition<TEntity> : FakeQueryDefinition<TEntity>;

    private sealed class BespokeDefinition : FakeQueryDefinition<FakeEntity>
    {
        public BespokeDefinition(string typeName) => TypeName = typeName;

        public string TypeName { get; }
    }
}
