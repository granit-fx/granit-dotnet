using Granit.Entities.Relations;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class RelationBuilderTests
{
    private sealed class Party
    {
        public Guid Id { get; init; }
        public IEnumerable<Address> Addresses { get; init; } = [];
        public PrimaryContact? Primary { get; init; }
        public IEnumerable<Invoice> Invoices { get; init; } = [];
    }

    private sealed class Address
    {
        public Guid Id { get; init; }
        public string City { get; init; } = string.Empty;
    }

    private sealed class PrimaryContact
    {
        public Guid Id { get; init; }
    }

    private sealed class PartyDefinition : EntityDefinition<Party>
    {
        public override string Name => "Test.Party";
        protected override void Configure(EntityDefinitionBuilder<Party> builder) =>
            builder
                .HasMany<Address>(p => p.Addresses, r => r
                    .DisplayAs(RelationDisplay.Tab)
                    .DisplayKey("Relation:Party.Addresses")
                    .Order(1))
                .HasOne<PrimaryContact>(p => p.Primary, r => r
                    .DisplayAs(RelationDisplay.Sidebar)
                    .Order(0));
    }

    [Fact]
    public void HasMany_records_a_Many_relation()
    {
        EntityDefinitionDescriptor d = new PartyDefinition().Descriptor;

        RelationDescriptor addresses = d.Relations.Single(r => r.Name == "Addresses");
        addresses.Cardinality.ShouldBe(RelationCardinality.Many);
        addresses.Display.ShouldBe(RelationDisplay.Tab);
        addresses.DisplayKey.ShouldBe("Relation:Party.Addresses");
        addresses.TargetEntityClrType.ShouldBe(typeof(Address));
    }

    [Fact]
    public void HasOne_records_a_One_relation()
    {
        EntityDefinitionDescriptor d = new PartyDefinition().Descriptor;

        RelationDescriptor primary = d.Relations.Single(r => r.Name == "Primary");
        primary.Cardinality.ShouldBe(RelationCardinality.One);
        primary.Display.ShouldBe(RelationDisplay.Sidebar);
    }

    [Fact]
    public void Relations_are_sorted_by_order_then_name()
    {
        EntityDefinitionDescriptor d = new PartyDefinition().Descriptor;

        d.Relations.Select(r => r.Name).ShouldBe(["Primary", "Addresses"]);
    }

    [Fact]
    public void HasMany_throws_on_non_property_lambda()
    {
        Should.Throw<ArgumentException>(() =>
            _ = new InvalidNonPropertySelectorDef().Descriptor);
    }

    [Fact]
    public void HasMany_aggregate_count_records_a_count_aggregate()
    {
        AggregateDef def = new();
        EntityDefinitionDescriptor d = def.Descriptor;
        RelationDescriptor invoices = d.Relations.Single();

        invoices.Aggregates.Count.ShouldBe(2);
        invoices.Aggregates[0].Kind.ShouldBe(RelationAggregateKind.Count);
        invoices.Aggregates[1].Kind.ShouldBe(RelationAggregateKind.Sum);
        invoices.Aggregates[1].PropertyName.ShouldBe("Amount");
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class AggregateDef : EntityDefinition<Party>
    {
        public override string Name => "Test.Party.Aggregates";
        protected override void Configure(EntityDefinitionBuilder<Party> builder) =>
            builder.HasMany<Invoice>(p => p.Invoices,
                r => r.DisplayAs(RelationDisplay.SmartButton)
                    .Aggregate(a => a.Count().Sum(i => i.Amount)));
    }

    private sealed class InvalidNonPropertySelectorDef : EntityDefinition<Party>
    {
        public override string Name => "Test.Party.Bad";
        protected override void Configure(EntityDefinitionBuilder<Party> builder) =>
            builder.HasMany<Address>(p => p.Addresses.Where(a => a.City == "Paris"));
    }

    [Fact]
    public void OnKanbanCard_defaults_false_and_opt_in_sets_flag()
    {
        EntityDefinitionDescriptor d = new KanbanPinnedDefinition().Descriptor;

        RelationDescriptor pinned = d.Relations.Single(r => r.Name == "Addresses");
        RelationDescriptor unpinned = d.Relations.Single(r => r.Name == "Primary");

        pinned.ShowOnKanbanCard.ShouldBeTrue();
        unpinned.ShowOnKanbanCard.ShouldBeFalse();
    }

    private sealed class KanbanPinnedDefinition : EntityDefinition<Party>
    {
        public override string Name => "Test.Party.KanbanPinned";
        protected override void Configure(EntityDefinitionBuilder<Party> builder) =>
            builder
                .HasMany<Address>(p => p.Addresses, r => r
                    .DisplayAs(RelationDisplay.SmartButton)
                    .OnKanbanCard())
                .HasOne<PrimaryContact>(p => p.Primary, r => r
                    .DisplayAs(RelationDisplay.Sidebar));
    }
}
